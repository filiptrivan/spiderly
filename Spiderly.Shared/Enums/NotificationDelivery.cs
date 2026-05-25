namespace Spiderly.Shared.Enums
{
    /// <summary>
    /// Durability guarantee for delivering a notification. Declared on the notification itself
    /// (<see cref="Spiderly.Shared.Interfaces.INotification.Delivery"/>) so a must-not-lose notification
    /// is locked to the safe mode in one place and can't be sent the unsafe way by a careless call site.
    /// </summary>
    public enum NotificationDelivery
    {
        /// <summary>
        /// Enqueue delivery to Hangfire immediately. Durable + auto-retried, but fires even if a surrounding
        /// transaction later rolls back. Use for loss-tolerant messages (ops pings, back-in-stock, price-drop).
        /// The default.
        /// </summary>
        FireNow = 0,

        /// <summary>
        /// Write to the transactional outbox inside the current transaction; a recurring sweep delivers it
        /// only after the transaction commits, and it survives a process crash. Use for must-not-lose messages
        /// tied to a state change (order confirmation, payment receipt, money/token notifications).
        /// </summary>
        Outbox = 1,
    }
}
