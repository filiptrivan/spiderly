using Hangfire;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class SecurityEventNotificationJob
    {
        private readonly IEmailingService _emailingService;
        private readonly ILogger<SecurityEventNotificationJob> _logger;
        private readonly INotificationSettings _notificationSettings;
        private readonly TelegramNotifier _telegramNotifier;

        public SecurityEventNotificationJob(IEmailingService emailingService, ILogger<SecurityEventNotificationJob> logger, INotificationSettings notificationSettings, TelegramNotifier telegramNotifier)
        {
            _emailingService = emailingService;
            _logger = logger;
            _notificationSettings = notificationSettings;
            _telegramNotifier = telegramNotifier;
        }

        public async Task SendAsync(string eventType, string message)
        {
            bool hasEmailRecipients = _notificationSettings.UnhandledExceptionRecipients?.Count > 0;
            bool hasTelegram = _telegramNotifier.IsConfigured;

            if (!hasEmailRecipients && !hasTelegram)
            {
                _logger.LogWarning("Security event '{EventType}' not sent — no notification channels configured", eventType);
                return;
            }

            if (hasEmailRecipients)
                await SendEmailAsync(eventType, message);

            if (hasTelegram)
            {
                string text = $"[{_notificationSettings.ApplicationName}] {eventType}\n{message}";
                await _telegramNotifier.SendAsync(text);
            }
        }

        private async Task SendEmailAsync(string eventType, string message)
        {
            try
            {
                string subject = $"{_notificationSettings.ApplicationName}: {eventType}";
                string body = message.Replace("\n", "<br>");

                await _emailingService.SendEmailAsync(
                    _notificationSettings.UnhandledExceptionRecipients,
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
