using System.Text.Json;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.IntegrationEvents
{
    /// <summary>
    /// Outbox payload for one delivered integration-event handler. Extends <see cref="OutboxEnvelope"/> (the event's
    /// code + data) with the target handler's <see cref="IIntegrationEventHandler.Code"/> — harvest writes one row per
    /// handler, so each is addressed and retried independently.
    /// </summary>
    public sealed class IntegrationEventOutboxPayload : OutboxEnvelope
    {
        /// <summary>The <see cref="IIntegrationEventHandler.Code"/> this row delivers to.</summary>
        public string TargetHandlerCode { get; set; } = null!; // Always set at harvest; materialized by the JSON deserializer at delivery
    }

    /// <summary>
    /// The single <see cref="IOutboxHandler"/> that delivers integration-event outbox rows. Each row targets ONE handler
    /// (harvest fans out one row per registered <see cref="IIntegrationEventHandler"/>): this rebuilds the event from its
    /// <see cref="OutboxEnvelope"/> via the shared <see cref="CodeTypeRegistry{TMarker}"/>, resolves the handler whose
    /// <see cref="IIntegrationEventHandler.Code"/> matches the row, and invokes it. A throw retries only this row — the
    /// failing handler — never its siblings, so handlers no longer need to be idempotent against each other.
    /// </summary>
    public class IntegrationEventOutboxHandler : IOutboxHandler
    {
        /// <summary>The <see cref="IOutboxMessage.HandlerCode"/> this handler claims.</summary>
        public const string HandlerCode = "IntegrationEvent";

        private readonly CodeTypeRegistry<IIntegrationEvent> _registry;
        private readonly IEnumerable<IIntegrationEventHandler> _handlers;

        /// <summary>Creates the handler over the delivery-side registry and the registered event handlers (resolved per outbox-sweep scope).</summary>
        public IntegrationEventOutboxHandler(
            CodeTypeRegistry<IIntegrationEvent> registry,
            IEnumerable<IIntegrationEventHandler> handlers)
        {
            _registry = registry;
            _handlers = handlers;
        }

        /// <inheritdoc/>
        public string Code => HandlerCode;

        /// <inheritdoc/>
        public async Task HandleAsync(string payload, CancellationToken cancellationToken)
        {
            IntegrationEventOutboxPayload p = JsonSerializer.Deserialize<IntegrationEventOutboxPayload>(payload)
                ?? throw new InvalidOperationException("Integration-event outbox payload deserialized to null.");

            IIntegrationEvent integrationEvent = _registry.Rebuild(p.Code, p.Data);
            Type eventType = integrationEvent.GetType();

            IIntegrationEventHandler handler = _handlers
                .SingleOrDefault(h => h.EventType == eventType && h.Code == p.TargetHandlerCode)
                ?? throw new InvalidOperationException(
                    $"No IIntegrationEventHandler with Code '{p.TargetHandlerCode}' for event '{p.Code}' — " +
                    "the handler was removed or renamed after the row was harvested.");

            // Throwing retries THIS row only (this one handler), under its own backoff/cap. Siblings are separate rows.
            await handler.HandleAsync(integrationEvent, cancellationToken);
        }
    }
}
