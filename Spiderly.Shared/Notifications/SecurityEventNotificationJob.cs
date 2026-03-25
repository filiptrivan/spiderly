using Hangfire;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class SecurityEventNotificationJob
    {
        private readonly IEmailingService _emailingService;
        private readonly ILogger<SecurityEventNotificationJob> _logger;

        public SecurityEventNotificationJob(IEmailingService emailingService, ILogger<SecurityEventNotificationJob> logger)
        {
            _emailingService = emailingService;
            _logger = logger;
        }

        public async Task SendAsync(string eventType, string message)
        {
            bool hasEmailRecipients = SettingsProvider.Current.UnhandledExceptionRecipients?.Count > 0;
            bool hasTelegram = Helper.IsTelegramConfigured();

            if (!hasEmailRecipients && !hasTelegram)
            {
                _logger.LogWarning("Security event '{EventType}' not sent — no notification channels configured", eventType);
                return;
            }

            if (hasEmailRecipients)
                await SendEmailAsync(eventType, message);

            if (hasTelegram)
            {
                string text = $"[{SettingsProvider.Current.ApplicationName}] {eventType}\n{message}";
                await Helper.SendTelegramNotificationAsync(text, _logger);
            }
        }

        private async Task SendEmailAsync(string eventType, string message)
        {
            try
            {
                string subject = $"{SettingsProvider.Current.ApplicationName}: {eventType}";
                string body = message.Replace("\n", "<br>");

                await _emailingService.SendEmailAsync(
                    SettingsProvider.Current.UnhandledExceptionRecipients,
                    subject,
                    body
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Security event email not sent for {EventType}", eventType);
            }
        }
    }
}
