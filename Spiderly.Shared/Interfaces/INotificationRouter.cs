namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Decides which channels a notification is routed to. The default implementation is configured in code
    /// (a per-notification-type → channel-set map registered at startup); a consumer that wants runtime control
    /// can swap in an implementation backed by a database table exposed in the admin panel.
    ///
    /// <para>The returned set is the routing intent only. The dispatcher still intersects it with what the
    /// notification actually implements (its channel content interfaces) and each channel's
    /// <see cref="INotificationChannel.IsConfigured"/>, so a routed-but-unsupported or unconfigured channel is a
    /// no-op rather than an error.</para>
    /// </summary>
    public interface INotificationRouter
    {
        /// <summary>Returns the channels <paramref name="notification"/> should be delivered through.</summary>
        IReadOnlyCollection<INotificationChannel> ChannelsFor(INotification notification);
    }
}
