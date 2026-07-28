using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Shared.Authorization;
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
        private readonly TokenKeyOptions _tokenKeySettings;
        private readonly CookieManager _cookieManager;
        private readonly ISpiderlyPrincipalAccessor _principalAccessor;

        public SpiderlyExceptionHandler(
            ILogger<SpiderlyExceptionHandler> logger,
            IStringLocalizer localizer,
            IWebHostEnvironment env,
            IOptions<TokenKeyOptions> tokenKeyOptions,
            CookieManager cookieManager,
            ISpiderlyPrincipalAccessor principalAccessor)
        {
            _logger = logger;
            _localizer = localizer;
            _env = env;
            _tokenKeySettings = tokenKeyOptions.Value;
            _cookieManager = cookieManager;
            _principalAccessor = principalAccessor;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
        {
            httpContext.Response.ContentType = "application/json";

            string? exceptionString = _env.IsDevelopment() ? ex.ToString() : null;
            long? principalId = _principalAccessor.Current.PrincipalId;

            ApiErrorDTO body;
            // Single source of truth for the level — the handler's logging and SpiderlyExceptionClassifier.IsExpected
            // (consumed by error-tracker filters like Sentry's beforeSend) derive from one place, so they can't drift.
            LogLevel logLevel = SpiderlyExceptionClassifier.GetLogLevel(ex);
            bool logException;

            if (ex is BusinessException businessEx)
            {
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                body = new ApiErrorDTO { Message = businessEx.Message, ErrorCode = businessEx.ErrorCode };
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
                logException = false;
            }
            else if (ex is ExpiredVerificationException expiredVerificationEx)
            {
                httpContext.Response.StatusCode = expiredVerificationEx.StatusCode;
                body = new ApiErrorDTO { Message = expiredVerificationEx.Message };
                logException = false;
            }
            else if (ex is UnauthorizedException unauthorizedEx)
            {
                httpContext.Response.StatusCode = unauthorizedEx.StatusCode;
                body = new ApiErrorDTO { Message = unauthorizedEx.Message };
                logException = false;
            }
            else if (ex is PrincipalKindMismatchException)
            {
                // Deliberately generic to the caller: which principal kinds a path accepts is not something a
                // client needs (or should be told). The specifics are in the log line above.
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                body = new ApiErrorDTO { Message = _localizer["UnauthorizedAccessExceptionMessage"] };
                logException = false;
            }
            else if (ex is SecurityViolationException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                body = new ApiErrorDTO { Message = _localizer["GlobalError"] };
                logException = true; // Error-level log — the app's error tracker is the alert channel.
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
                logException = true;
            }
            else if (ex is DbUpdateException dbUpdateEx
                && SpiderlyExceptionClassifier.GetDbConstraintErrorCode(dbUpdateEx) is string constraintCode)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                // Exhaustive on the codes GetDbConstraintErrorCode returns — a future code falls to the
                // generic message rather than being silently mislabeled as a unique-constraint violation.
                string constraintMessageKey = constraintCode switch
                {
                    ApiErrorCodes.UniqueViolation => "UniqueConstraintException",
                    ApiErrorCodes.ForeignKeyViolation => "ForeignKeyConstraintException",
                    _ => "GlobalError",
                };
                body = new ApiErrorDTO
                {
                    Message = _localizer[constraintMessageKey],
                    ErrorCode = constraintCode,
                };
                logException = true;
            }
            else
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                body = new ApiErrorDTO { Message = _localizer["GlobalError"] };
                // Error-level log + the framework's exception diagnostic are the alerting surface — an error
                // tracker (e.g. Sentry.AspNetCore) captures both without any Spiderly seam.
                logException = true;
            }

            body.StatusCode = httpContext.Response.StatusCode;
            body.Exception = exceptionString;

            // Reference shown ⟺ event reaches the error tracker: logLevel >= Error is exactly
            // SpiderlyExceptionClassifier.IsExpected == false (the level was already classified above), so a
            // user never reads out a reference that finds nothing, and an expected 4xx never looks like an incident.
            if (logLevel >= LogLevel.Error)
                body.TraceId = TraceCorrelation.CurrentTraceId();

            if (logException)
                _logger.Log(logLevel, ex, "Currently authenticated principal id: {PrincipalId}", principalId);
            else
                _logger.Log(logLevel, "{ExceptionType}: {Message} (principal id: {PrincipalId})", ex.GetType().Name, ex.Message, principalId);

            await httpContext.Response.WriteAsJsonAsync(body, cancellationToken);

            return true;
        }
    }
}
