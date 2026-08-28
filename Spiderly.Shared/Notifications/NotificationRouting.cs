using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Immutable, code-first routing map: notification type → the channel codes it is delivered through. Built by
    /// <see cref="NotificationRoutingBuilder"/> at startup and registered as a singleton; consumed by
    /// <see cref="DefaultNotificationRouter"/>.
    /// </summary>
    public class NotificationRoutingMap
    {
        /// <summary>Creates the map from a built notification-type → channel-codes dictionary.</summary>
        public NotificationRoutingMap(IReadOnlyDictionary<Type, List<string>> routes)
        {
            Routes = routes;
        }

        /// <summary>Notification type → channel codes it routes to.</summary>
        public IReadOnlyDictionary<Type, List<string>> Routes { get; }
    }

    /// <summary>
    /// Fluent builder for the notification routing map, used inside <c>spiderly.AddNotifications(...)</c>.
    /// <example>
    /// <code>
    /// spiderly.AddNotifications(r => r
    ///     .Route&lt;OrderShippedNotification&gt;().To("Email").To("Telegram"));
    /// </code>
    /// </example>
    /// </summary>
    public class NotificationRoutingBuilder
    {
        private readonly Dictionary<Type, List<string>> _routes = new();

        /// <summary>Begins a route for <typeparamref name="TNotification"/>; chain <c>.To(channelCode)</c> to add channels.</summary>
        public NotificationRouteBuilder Route<TNotification>()
            where TNotification : INotification
        {
            if (!_routes.TryGetValue(typeof(TNotification), out List<string>? codes))
            {
                codes = new List<string>();
                _routes[typeof(TNotification)] = codes;
            }
            return new NotificationRouteBuilder(this, codes);
        }

        /// <summary>
        /// Builds the immutable routing map. Called by the framework at startup; public so consumer tests can pin
        /// their routing config (e.g. "every registered <see cref="IEmailRenderer"/>'s notification type is routed") —
        /// an unrouted notification is dropped silently, so without such a pin the gap is invisible.
        /// </summary>
        public NotificationRoutingMap Build() => new(_routes);
    }

    /// <summary>The per-notification continuation of <see cref="NotificationRoutingBuilder"/>; adds channel codes.</summary>
    public class NotificationRouteBuilder
    {
        private readonly NotificationRoutingBuilder _parent;
        private readonly List<string> _codes;

        internal NotificationRouteBuilder(NotificationRoutingBuilder parent, List<string> codes)
        {
            _parent = parent;
            _codes = codes;
        }

        /// <summary>Adds a channel (by its <see cref="INotificationChannel.Code"/>) to the current notification's route.</summary>
        public NotificationRouteBuilder To(string channelCode)
        {
            _codes.Add(channelCode);
            return this;
        }

        /// <summary>Starts a route for another notification type (fluent chaining).</summary>
        public NotificationRouteBuilder Route<TNotification>()
            where TNotification : INotification
            => _parent.Route<TNotification>();
    }
}
