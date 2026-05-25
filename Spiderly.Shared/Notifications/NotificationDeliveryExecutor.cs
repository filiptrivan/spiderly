using System.Text.Json;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// Shared delivery core used by both async paths — the <see cref="NotificationDeliveryJob"/> (FireNow) and the
    /// <see cref="NotificationOutboxHandler"/> (Outbox). Rebuilds the notification from its code + JSON, reloads the
    /// recipient (if any) via the registered resolver, finds the target channel by code, and sends. One call =
    /// one (notification, recipient, channel), so a failure isolates and retries that single channel.
    /// </summary>
    public class NotificationDeliveryExecutor
    {
        private readonly NotificationTypeRegistry _registry;
        private readonly IEnumerable<INotificationChannel> _channels;
        private readonly IEnumerable<INotificationRecipientResolver> _recipientResolvers;
        private readonly ILogger<NotificationDeliveryExecutor> _logger;

        /// <summary>Creates the executor. <paramref name="recipientResolvers"/> is empty when the app sends admin-only notifications.</summary>
        public NotificationDeliveryExecutor(
            NotificationTypeRegistry registry,
            IEnumerable<INotificationChannel> channels,
            IEnumerable<INotificationRecipientResolver> recipientResolvers,
            ILogger<NotificationDeliveryExecutor> logger)
        {
            _registry = registry;
            _channels = channels;
            _recipientResolvers = recipientResolvers;
            _logger = logger;
        }

        /// <summary>Delivers one notification to one channel. Throws on unknown code/channel or a missing resolver so the caller (Hangfire/outbox) retries.</summary>
        public async Task DeliverAsync(string notificationCode, string notificationData, long? recipientId, string channelCode, CancellationToken cancellationToken)
        {
            Type notificationType = _registry.GetNotificationType(notificationCode);

            INotification notification = (INotification)JsonSerializer.Deserialize(notificationData, notificationType);
            if (notification == null)
                throw new InvalidOperationException($"Notification '{notificationCode}' payload deserialized to null.");

            INotificationChannel channel = _channels.FirstOrDefault(c => c.Code == channelCode)
                ?? throw new InvalidOperationException($"No notification channel registered with Code '{channelCode}'.");

            if (!channel.IsConfigured)
            {
                // Channel not set up in this environment (e.g. missing email API key). Don't throw — there's
                // nothing to deliver to — but log it: this is otherwise an invisible swallow that could hide the
                // very exception/security alerts you'd want to see when an environment is misconfigured.
                _logger.LogWarning(
                    "Notification '{NotificationCode}' is routed to channel '{ChannelCode}', but that channel is not configured in this environment — skipping delivery.",
                    notificationCode, channelCode);
                return;
            }

            INotificationRecipient recipient = null;
            if (recipientId.HasValue)
            {
                INotificationRecipientResolver resolver = _recipientResolvers.FirstOrDefault()
                    ?? throw new InvalidOperationException(
                        "A recipient was specified but no INotificationRecipientResolver is registered. Register one to use Notify(recipient, ...).");

                recipient = await resolver.ResolveAsync(recipientId.Value);
                if (recipient == null)
                    return; // recipient no longer exists — drop silently
            }

            await channel.SendAsync(notification, recipient, cancellationToken);
        }
    }
}
