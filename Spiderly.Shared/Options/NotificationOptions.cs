namespace Spiderly.Shared
{
    /// <summary>
    /// Notification options. Bound from the <c>AppSettings:Spiderly.Shared</c> configuration section and
    /// injected into the notification channels as <see cref="Microsoft.Extensions.Options.IOptions{T}"/>.
    /// </summary>
    public class NotificationOptions
    {
        /// <summary>Application name, used as a prefix in notification subjects/messages.</summary>
        public string? ApplicationName { get; set; }

        /// <summary>Email recipients for business notifications sent via <c>NotifyAdmins</c> (e.g. "a new account
        /// needs approval"). Operational telemetry (errors, security events, failed jobs) is not notified — it goes
        /// to logs and the app's error tracker.</summary>
        public List<string> AdminRecipients { get; set; } = new();
    }
}
