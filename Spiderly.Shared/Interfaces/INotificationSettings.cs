namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the operational-notification settings (recipients, Telegram channel, rate
    /// limiting). Implemented by <see cref="Settings"/> and injected into the notification jobs/filters,
    /// so they depend on configuration passed in rather than the global mutable
    /// <c>SettingsProvider</c> static.
    /// </summary>
    public interface INotificationSettings
    {
        /// <summary>Application name, used as a prefix in notification subjects/messages.</summary>
        string ApplicationName { get; }

        /// <summary>Email recipients for unhandled-exception and security-event notifications.</summary>
        List<string> UnhandledExceptionRecipients { get; }

        /// <summary>Telegram bot token; when set (with <see cref="TelegramChatId"/>) Telegram alerts are enabled.</summary>
        string TelegramBotToken { get; }

        /// <summary>Telegram chat id alerts are sent to.</summary>
        string TelegramChatId { get; }

        /// <summary>Minimum minutes between duplicate notifications, to throttle alert storms.</summary>
        int NotificationRateLimitMinutes { get; }
    }
}
