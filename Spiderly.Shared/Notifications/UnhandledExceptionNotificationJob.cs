using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    [AutomaticRetry(Attempts = 3, DelaysInSeconds = new[] { 30, 60, 120 })]
    public class UnhandledExceptionNotificationJob
    {
        private readonly IEmailingService _emailingService;
        private readonly ILogger<UnhandledExceptionNotificationJob> _logger;
        private readonly NotificationOptions _notificationSettings;
        private readonly TelegramNotifier _telegramNotifier;

        public UnhandledExceptionNotificationJob(IEmailingService emailingService, ILogger<UnhandledExceptionNotificationJob> logger, IOptions<NotificationOptions> notificationOptions, TelegramNotifier telegramNotifier)
        {
            _emailingService = emailingService;
            _logger = logger;
            _notificationSettings = notificationOptions.Value;
            _telegramNotifier = telegramNotifier;
        }

        public async Task SendAsync(long? userId, string exceptionString)
        {
            bool hasEmailRecipients = _notificationSettings.UnhandledExceptionRecipients?.Count > 0;
            bool hasTelegram = _telegramNotifier.IsConfigured;

            if (!hasEmailRecipients && !hasTelegram)
            {
                _logger.LogWarning("Unhandled exception notification not sent — no notification channels configured; User ID: {UserId}", userId);
                return;
            }

            if (hasEmailRecipients)
                await SendUnhandledExceptionEmailAsync(userId, exceptionString);

            if (hasTelegram)
                await _telegramNotifier.SendUnhandledExceptionAsync(userId, exceptionString);
        }

        private async Task SendUnhandledExceptionEmailAsync(long? userId, string exceptionString)
        {
            try
            {
                string subject = $"{_notificationSettings.ApplicationName}: Unhandled Exception";
                string body = $$"""
Currently authenticated user id: {{userId}}); <br>
{{exceptionString}}
""";

                await _emailingService.SendEmailAsync(
                    _notificationSettings.UnhandledExceptionRecipients,
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
