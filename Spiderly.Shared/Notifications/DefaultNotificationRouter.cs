using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Code-first <see cref="INotificationRouter"/>: looks the notification's type up in the
    /// <see cref="NotificationRoutingMap"/> and returns the registered channels (matched by
    /// <see cref="INotificationChannel.Code"/>). A notification with no route returns no channels. Swap this for a
    /// database/admin-backed implementation to make routing runtime-configurable.
    /// </summary>
    public class DefaultNotificationRouter : INotificationRouter
    {
        private readonly NotificationRoutingMap _map;
        private readonly IEnumerable<INotificationChannel> _channels;

        /// <summary>Creates the router over the routing map and the registered channels.</summary>
        public DefaultNotificationRouter(NotificationRoutingMap map, IEnumerable<INotificationChannel> channels)
        {
            _map = map;
            _channels = channels;
        }

        /// <inheritdoc/>
        public IReadOnlyCollection<INotificationChannel> ChannelsFor(INotification notification)
        {
            if (!_map.Routes.TryGetValue(notification.GetType(), out List<string>? codes) || codes.Count == 0)
                return Array.Empty<INotificationChannel>();

            return _channels.Where(c => codes.Contains(c.Code)).ToList();
        }
    }
}
