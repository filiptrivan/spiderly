namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Assigns a stable code to a notification type so it can be persisted (outbox row / Hangfire job) and rebuilt
    /// later regardless of class renames or moves. Required on any notification that may be delivered via
    /// <see cref="Spiderly.Shared.Enums.NotificationDelivery.Outbox"/> or asynchronously; the startup registry maps
    /// code → type.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class NotificationCodeAttribute : Attribute
    {
        /// <summary>Assigns the stable notification code.</summary>
        /// <param name="code">A short, stable identifier (e.g. <c>"OrderShipped"</c>). Must be unique across notifications.</param>
        public NotificationCodeAttribute(string code)
        {
            Code = code;
        }

        /// <summary>The stable notification code.</summary>
        public string Code { get; }
    }
}
