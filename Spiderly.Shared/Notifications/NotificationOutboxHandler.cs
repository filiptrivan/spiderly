using System.Text.Json;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Notifications
{
    /// <summary>
    /// What the notification framework stores in a transactional-outbox row for an
    /// <see cref="Spiderly.Shared.Enums.NotificationDelivery.Outbox"/> notification — the shared
    /// <see cref="OutboxEnvelope"/> (code + data) plus the channel-fan-out fields. One row per channel; the
    /// recipient lives here (by id), never on the general outbox row.
    /// </summary>
    public sealed class NotificationOutboxPayload : OutboxEnvelope
    {
        /// <summary>The target recipient's id, or <c>null</c> for an admin notification.</summary>
        public long? RecipientId { get; set; }

        /// <summary>The target channel's <see cref="INotificationChannel.Code"/>.</summary>
        public string ChannelCode { get; set; } = null!; // Always set at enqueue; materialized by the JSON deserializer at delivery
    }

    /// <summary>
    /// The <see cref="IOutboxHandler"/> that delivers <see cref="Spiderly.Shared.Enums.NotificationDelivery.Outbox"/>
    /// notifications — the notification framework's single consumer of the general outbox. Delegates to the shared
    /// <see cref="NotificationDeliveryExecutor"/>.
    /// </summary>
    public class NotificationOutboxHandler : IOutboxHandler
    {
        /// <summary>The <see cref="IOutboxMessage.HandlerCode"/> this handler claims.</summary>
        public const string HandlerCode = "Notification";

        private readonly NotificationDeliveryExecutor _executor;

        /// <summary>Creates the handler over the shared delivery executor.</summary>
        public NotificationOutboxHandler(NotificationDeliveryExecutor executor)
        {
            _executor = executor;
        }

        /// <inheritdoc/>
        public string Code => HandlerCode;

        /// <inheritdoc/>
        public async Task HandleAsync(string payload, CancellationToken cancellationToken)
        {
            NotificationOutboxPayload p = JsonSerializer.Deserialize<NotificationOutboxPayload>(payload)
                ?? throw new InvalidOperationException("Notification outbox payload deserialized to null.");

            await _executor.DeliverAsync(p.Code, p.Data, p.RecipientId, p.ChannelCode, cancellationToken);
        }
    }
}
