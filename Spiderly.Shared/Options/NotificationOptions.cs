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

        /// <summary>Email recipients for business notifications sent via <c>NotifyAdmins</c> (e.g. "a new account
        /// needs approval"). Operational telemetry (errors, security events, failed jobs) is not notified — it goes
        /// to logs and the app's error tracker.</summary>
        public List<string> AdminRecipients { get; set; }

        /// <summary>Minimum minutes between duplicate notifications, to throttle alert storms.</summary>
        public int NotificationRateLimitMinutes { get; set; } = 5;
    }
}
