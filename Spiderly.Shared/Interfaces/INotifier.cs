namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Entry point for sending notifications. Both methods are non-blocking: they hand the work off according to
    /// the notification's <see cref="INotification.Delivery"/> level — <c>FireNow</c> enqueues delivery to Hangfire
    /// per routed channel; <c>Outbox</c> stages a transactional-outbox row inside the current transaction (so it
    /// must be called inside <c>WithTransactionAsync</c>, like <see cref="IOutbox"/>).
    /// </summary>
    public interface INotifier
    {
        /// <summary>
        /// Sends <paramref name="notification"/> to a specific recipient. Each routed channel reads the address
        /// from the recipient's capability interface and skips the recipient if it has no address for that channel.
        /// </summary>
        /// <param name="recipient">The target; supplies its own per-channel address.</param>
        /// <param name="notification">The notification to route and deliver.</param>
        void Notify(INotificationRecipient recipient, INotification notification);

        /// <summary>
        /// Sends <paramref name="notification"/> to the operators/team. Routed channels use their configured static
        /// recipients (e.g. an admin email list, a Telegram chat) — there is no per-message recipient.
        /// </summary>
        /// <param name="notification">The notification to route and deliver.</param>
        void NotifyAdmins(INotification notification);
    }
}
