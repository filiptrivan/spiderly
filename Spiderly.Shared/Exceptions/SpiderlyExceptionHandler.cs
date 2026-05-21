using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;

namespace Spiderly.Shared.Exceptions
{
    public class SpiderlyExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<SpiderlyExceptionHandler> _logger;
        private readonly IStringLocalizer _localizer;
        private readonly IWebHostEnvironment _env;
        private readonly ITokenKeySettings _tokenKeySettings;
        private readonly CookieManager _cookieManager;

        public SpiderlyExceptionHandler(
            ILogger<SpiderlyExceptionHandler> logger,
            IStringLocalizer localizer,
            IWebHostEnvironment env,
            ITokenKeySettings tokenKeySettings,
            CookieManager cookieManager)
        {
            _logger = logger;
            _localizer = localizer;
            _env = env;
            _tokenKeySettings = tokenKeySettings;
            _cookieManager = cookieManager;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
        {
            httpContext.Response.ContentType = "application/json";

            string exceptionString = _env.IsDevelopment() ? ex.ToString() : null;
            long? userId = Helper.GetCurrentUserIdOrDefault(httpContext);

            ApiErrorDTO body;
            LogLevel logLevel;
            bool logException;

            if (ex is BusinessException businessEx)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                body = new ApiErrorDTO { Message = businessEx.Message };
                logLevel = LogLevel.Information;
                logException = false;
            }
            else if (ex is SpiderlyValidationException || ex is ValidationException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
                body = new ApiErrorDTO
                {
                    Message = _localizer["ValidationFailed"],
                    ErrorCode = ApiErrorCodes.ValidationFailed,
                    FieldErrors = ex is SpiderlyValidationException sve
                        ? sve.Errors
                        : ((ValidationException)ex).Errors
                            .GroupBy(f => f.PropertyName)
                            .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToArray()),
                };
                logLevel = LogLevel.Information;
                logException = false;
            }
            else if (ex is ExpiredVerificationException expiredVerificationEx)
            {
                httpContext.Response.StatusCode = expiredVerificationEx.StatusCode;
                body = new ApiErrorDTO { Message = expiredVerificationEx.Message };
                logLevel = LogLevel.Information;
                logException = false;
            }
            else if (ex is UnauthorizedException unauthorizedEx)
            {
                httpContext.Response.StatusCode = unauthorizedEx.StatusCode;
                body = new ApiErrorDTO { Message = unauthorizedEx.Message };
                logLevel = LogLevel.Warning;
                logException = false;
            }
            else if (ex is SecurityViolationException securityViolationEx)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                body = new ApiErrorDTO { Message = _localizer["GlobalError"] };
                logLevel = LogLevel.Error;
                logException = true;

                if (!_env.IsDevelopment())
                {
                    httpContext.RequestServices.GetService<INotificationDispatcher>()
                        ?.DispatchSecurityEvent("SecurityViolation", securityViolationEx.Message, $"User {userId}: {securityViolationEx.Message}");
                }
            }
            else if (ex is SecurityTokenException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                httpContext.Response.Headers.WWWAuthenticate = $"Bearer error=\"{ApiErrorCodes.InvalidToken}\"";
                body = new ApiErrorDTO
                {
                    Message = _localizer["TokenExpired"],
                    ErrorCode = ApiErrorCodes.InvalidToken,
                };
                logLevel = LogLevel.Information;
                logException = false;

                _cookieManager.ClearCookie(httpContext.Response.Cookies, _tokenKeySettings.AccessTokenKey, httpOnly: true);
                _cookieManager.ClearCookie(httpContext.Response.Cookies, _tokenKeySettings.RefreshTokenKey, httpOnly: true);
                _cookieManager.ClearCookie(httpContext.Response.Cookies, _tokenKeySettings.AuthResultKey, httpOnly: false);
            }
            else if (ex is DbUpdateConcurrencyException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                body = new ApiErrorDTO
                {
                    Message = _localizer["ConcurrencyException"],
                    ErrorCode = ApiErrorCodes.ConcurrencyConflict,
                };
                logLevel = LogLevel.Warning;
                logException = true;
            }
            else if (ex is DbUpdateException dbUpdateEx && TryMapDbConstraint(dbUpdateEx, out string constraintCode, out string constraintMessageKey))
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                body = new ApiErrorDTO
                {
                    Message = _localizer[constraintMessageKey],
                    ErrorCode = constraintCode,
                };
                logLevel = LogLevel.Warning;
                logException = true;
            }
            else
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                body = new ApiErrorDTO { Message = _localizer["GlobalError"] };
                logLevel = LogLevel.Error;
                logException = true;

                if (!_env.IsDevelopment())
                {
                    httpContext.RequestServices.GetService<INotificationDispatcher>()
                        ?.DispatchUnhandledException(userId, ex);
                }
            }

            body.StatusCode = httpContext.Response.StatusCode;
            body.Exception = exceptionString;

            if (logException)
                _logger.Log(logLevel, ex, "Currently authenticated user id: {UserId}", userId);
            else
                _logger.Log(logLevel, "{ExceptionType}: {Message} (user id: {UserId})", ex.GetType().Name, ex.Message, userId);

            await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);

            return true;
        }

        private static bool TryMapDbConstraint(DbUpdateException ex, out string errorCode, out string messageKey)
        {
            const string postgresUniqueViolation = "23505";
            const string postgresForeignKeyViolation = "23503";
            const int sqlServerUniqueConstraint = 2627;
            const int sqlServerUniqueIndex = 2601;
            const int sqlServerForeignKey = 547;

            if (ex.InnerException is PostgresException pg)
            {
                switch (pg.SqlState)
                {
                    case postgresUniqueViolation:
                        errorCode = ApiErrorCodes.UniqueViolation;
                        messageKey = "UniqueConstraintException";
                        return true;
                    case postgresForeignKeyViolation:
                        errorCode = ApiErrorCodes.ForeignKeyViolation;
                        messageKey = "ForeignKeyConstraintException";
                        return true;
                }
            }

            if (ex.InnerException is SqlException sql)
            {
                switch (sql.Number)
                {
                    case sqlServerUniqueConstraint:
                    case sqlServerUniqueIndex:
                        errorCode = ApiErrorCodes.UniqueViolation;
                        messageKey = "UniqueConstraintException";
                        return true;
                    case sqlServerForeignKey:
                        errorCode = ApiErrorCodes.ForeignKeyViolation;
                        messageKey = "ForeignKeyConstraintException";
                        return true;
                }
            }

            errorCode = null;
            messageKey = null;
            return false;
        }
    }
}
