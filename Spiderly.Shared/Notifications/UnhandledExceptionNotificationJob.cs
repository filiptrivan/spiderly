using Hangfire;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System.Text;

namespace Spiderly.Shared.Notifications
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class UnhandledExceptionNotificationJob
    {
        private readonly IEmailingService _emailingService;
        private readonly ILogger<UnhandledExceptionNotificationJob> _logger;

        public UnhandledExceptionNotificationJob(IEmailingService emailingService, ILogger<UnhandledExceptionNotificationJob> logger)
        {
            _emailingService = emailingService;
            _logger = logger;
        }

        public async Task SendAsync(long? userId, string exceptionString)
        {
            if (SettingsProvider.Current.UnhandledExceptionRecipients?.Count > 0)
                await SendUnhandledExceptionEmailAsync(userId, exceptionString);

            if (Helper.IsTelegramConfigured())
                await Helper.SendTelegramNotificationAsync(userId, exceptionString, _logger);
        }

        private async Task SendUnhandledExceptionEmailAsync(long? userId, string exceptionString)
        {
            try
            {
                string subject = $"{SettingsProvider.Current.ApplicationName}: Unhandled Exception";
                string body = $$"""
Currently authenticated user id: {{userId}}); <br>
{{exceptionString}}
""";

                await _emailingService.SendEmailAsync(
                    SettingsProvider.Current.UnhandledExceptionRecipients,
                    subject,
                    body
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled Exception email is not sent; Currently authenticated user id: {userId});", userId);
            }
        }
    }
}
