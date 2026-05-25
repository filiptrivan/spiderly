namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// A delivery transport (Email, Telegram, GitHub, a coding agent, …). Implementations are discovered from DI
    /// (<c>IEnumerable&lt;INotificationChannel&gt;</c>), so adding a channel is one class + one registration, with
    /// no framework enum and no change to existing notifications. Spiderly core ships only an Email channel;
    /// everything else is consumer-written (optionally as a separate package).
    ///
    /// <para>A channel ships its own content interface (what a notification must implement to be sendable on it)
    /// and, for dynamic recipients, its own recipient interface (where it reads the address). <see cref="SendAsync"/>
    /// pattern-matches the notification (and recipient) onto those interfaces and silently skips anything that does
    /// not implement them — a notification only goes where it opted in.</para>
    /// </summary>
    public interface INotificationChannel
    {
        /// <summary>
        /// Short, stable, channel-owned identifier (e.g. <c>"Email"</c>, <c>"Telegram"</c>). Used only to name the
        /// channel in persisted/serialized delivery work (outbox rows, Hangfire jobs) and in routing config —
        /// content and recipient resolution use capability interfaces, not this code. Not a framework enum; each
        /// channel picks its own. Must be unique across registered channels.
        /// </summary>
        string Code { get; }

        /// <summary>
        /// Whether this channel has enough configuration to deliver (API key, bot token, sender, …).
        /// Unconfigured channels are skipped by the dispatcher rather than failing.
        /// </summary>
        bool IsConfigured { get; }

        /// <summary>
        /// Delivers <paramref name="notification"/>. When <paramref name="recipient"/> is non-null (a
        /// <see cref="INotifier.Notify"/> call), the channel reads the address from the recipient's capability
        /// interface; when it is null (a <see cref="INotifier.NotifyAdmins"/> call), the channel uses its configured
        /// static address. The channel must no-op if the notification (or a required recipient) does not implement
        /// this channel's capability interface.
        /// </summary>
        /// <param name="notification">The notification to render and send.</param>
        /// <param name="recipient">The dynamic recipient, or <c>null</c> for an admin/static-config send.</param>
        /// <param name="cancellationToken">Delivery cancellation token.</param>
        Task SendAsync(INotification notification, INotificationRecipient recipient, CancellationToken cancellationToken);
    }
}
