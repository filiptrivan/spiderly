namespace Spiderly.Shared
{
    /// <summary>
    /// Operational-notification options (recipients, Telegram channel, rate limiting). Bound from the
    /// <c>AppSettings:Spiderly.Shared</c> configuration section and injected into the notification
    /// jobs/filters as <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class NotificationOptions
    {
        /// <summary>Application name, used as a prefix in notification subjects/messages.</summary>
        public string ApplicationName { get; set; }

        /// <summary>Email recipients for unhandled-exception and security-event notifications.</summary>
        public List<string> UnhandledExceptionRecipients { get; set; }

        /// <summary>Telegram bot token; when set (with <see cref="TelegramChatId"/>) Telegram alerts are enabled.</summary>
        public string TelegramBotToken { get; set; }

        /// <summary>Telegram chat id alerts are sent to.</summary>
        public string TelegramChatId { get; set; }

        /// <summary>Minimum minutes between duplicate notifications, to throttle alert storms.</summary>
        public int NotificationRateLimitMinutes { get; set; } = 5;
    }
}
