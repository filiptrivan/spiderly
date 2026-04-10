using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Exceptions
{
    public class SpiderlyExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<SpiderlyExceptionHandler> _logger;
        private readonly IStringLocalizer _localizer;
        private readonly IWebHostEnvironment _env;

        public SpiderlyExceptionHandler(
            ILogger<SpiderlyExceptionHandler> logger,
            IStringLocalizer localizer,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _localizer = localizer;
            _env = env;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception ex, CancellationToken cancellationToken)
        {
            httpContext.Response.ContentType = "application/json";

            string exceptionString = "";

            if (_env.IsDevelopment())
                exceptionString = ex.ToString();

            string message;
            LogLevel logLevel;
            long? userId = Helper.GetCurrentUserIdOrDefault(httpContext);

            if (ex is BusinessException businessEx)
            {
                httpContext.Response.StatusCode = businessEx.StatusCode;
                message = businessEx.Message;
                logLevel = LogLevel.Warning;
            }
            else if (ex is ExpiredVerificationException expiredVerificationEx)
            {
                httpContext.Response.StatusCode = expiredVerificationEx.StatusCode;
                message = expiredVerificationEx.Message;
                logLevel = LogLevel.Information;
            }
            else if (ex is UnauthorizedException unauthorizedEx)
            {
                httpContext.Response.StatusCode = unauthorizedEx.StatusCode;
                message = unauthorizedEx.Message;
                logLevel = LogLevel.Error;
            }
            else if (ex is HackerException hackerEx)
            {
                httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                message = _localizer["GlobalError"];
                logLevel = LogLevel.Error;
                if (!_env.IsDevelopment())
                {
                    httpContext.RequestServices.GetService<INotificationDispatcher>()
                        ?.DispatchSecurityEvent("HackerException", hackerEx.Message, $"User {userId}: {hackerEx.Message}");
                }
            }
            else if (ex is SecurityTokenException securityTokenEx)
            {
                httpContext.Response.StatusCode = StatusCodes.Status419AuthenticationTimeout;
                message = securityTokenEx.Message;
                logLevel = LogLevel.Information;

                CookieHelper.ClearCookie(httpContext.Response.Cookies, SettingsProvider.Current.AccessTokenKey, httpOnly: true);
                CookieHelper.ClearCookie(httpContext.Response.Cookies, SettingsProvider.Current.RefreshTokenKey, httpOnly: true);
                CookieHelper.ClearCookie(httpContext.Response.Cookies, SettingsProvider.Current.AuthResultKey, httpOnly: false);
            }
            else if (ex is DbUpdateConcurrencyException)
            {
                httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
                message = _localizer["ConcurrencyException"];
                logLevel = LogLevel.Warning;
            }
            else
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                message = _localizer["GlobalError"];
                logLevel = LogLevel.Error;
                if (!_env.IsDevelopment())
                {
                    httpContext.RequestServices.GetService<INotificationDispatcher>()
                        ?.DispatchUnhandledException(userId, ex);
                }
            }

            _logger.Log(
                logLevel,
                ex,
                "Currently authenticated user id: {UserId}",
                userId
            );

            await httpContext.Response.WriteAsJsonAsync(new ApiErrorDTO
            {
                StatusCode = httpContext.Response.StatusCode,
                Message = message,
                Exception = exceptionString
            }, cancellationToken);

            return true;
        }
    }
}
