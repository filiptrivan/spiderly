using System.Text.Json;
using Hangfire;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Default <see cref="INotifier"/>. Applies dedupe, asks the router for the notification's channels, then fans
    /// out one delivery per channel according to the notification's <see cref="INotification.Delivery"/>:
    /// <c>FireNow</c> enqueues a Hangfire <see cref="NotificationDeliveryJob"/>; <c>Outbox</c> stages a row via
    /// <see cref="IOutbox"/> (must be inside <c>WithTransactionAsync</c>).
    /// </summary>
    public class Notifier : INotifier
    {
        private readonly INotificationRouter _router;
        private readonly NotificationTypeRegistry _registry;
        private readonly IBackgroundJobClient _backgroundJobClient;
        private readonly NotificationRateLimiter _rateLimiter;
        private readonly IEnumerable<IOutbox> _outboxes;

        /// <summary>Creates the notifier. <paramref name="outboxes"/> is empty unless <c>AddOutbox</c> was called.</summary>
        public Notifier(
            INotificationRouter router,
            NotificationTypeRegistry registry,
            IBackgroundJobClient backgroundJobClient,
            NotificationRateLimiter rateLimiter,
            IEnumerable<IOutbox> outboxes)
        {
            _router = router;
            _registry = registry;
            _backgroundJobClient = backgroundJobClient;
            _rateLimiter = rateLimiter;
            _outboxes = outboxes;
        }

        /// <inheritdoc/>
        public void Notify(INotificationRecipient recipient, INotification notification)
            => Dispatch(notification, recipient?.NotificationRecipientId);

        /// <inheritdoc/>
        public void NotifyAdmins(INotification notification)
            => Dispatch(notification, recipientId: null);

        private void Dispatch(INotification notification, long? recipientId)
        {
            string dedupeKey = notification.DedupeKey;
            if (dedupeKey != null && !_rateLimiter.ShouldSend(dedupeKey, notification.DedupeWindow))
                return;

            IReadOnlyCollection<INotificationChannel> channels = _router.ChannelsFor(notification);
            if (channels.Count == 0)
                return;

            // Serialized once and re-used per channel; rebuilt at delivery from the code + this JSON.
            string code = _registry.GetCode(notification.GetType());
            string data = JsonSerializer.Serialize(notification, notification.GetType());

            foreach (INotificationChannel channel in channels)
            {
                switch (notification.Delivery)
                {
                    case NotificationDelivery.FireNow:
                        _backgroundJobClient.Enqueue<NotificationDeliveryJob>(
                            job => job.DeliverAsync(code, data, recipientId, channel.Code));
                        break;

                    case NotificationDelivery.Outbox:
                        IOutbox outbox = _outboxes.FirstOrDefault()
                            ?? throw new InvalidOperationException(
                                $"Notification {notification.GetType().Name} uses NotificationDelivery.Outbox, but the outbox is not enabled. Call spiderly.AddOutbox<TOutbox>().");

                        outbox.Enqueue(NotificationOutboxHandler.HandlerCode, new NotificationOutboxPayload
                        {
                            NotificationCode = code,
                            NotificationData = data,
                            RecipientId = recipientId,
                            ChannelCode = channel.Code,
                        });
                        break;

                    default:
                        throw new InvalidOperationException($"Unhandled NotificationDelivery '{notification.Delivery}'.");
                }
            }
        }
    }
}
