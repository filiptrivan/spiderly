using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.IntegrationEvents
{
    /// <summary>
    /// Default <see cref="IIntegrationEventPublisher"/> — stages an <see cref="OutboxEnvelope"/> row on the current scoped
    /// <see cref="IOutbox"/> (the same DbContext the surrounding transaction writes to). The envelope reads the event's
    /// stable code off the type, so no registry is needed at the producer side.
    /// </summary>
    public class IntegrationEventPublisher : IIntegrationEventPublisher
    {
        private readonly IOutbox _outbox;

        /// <summary>Creates the publisher over the request/job-scoped <see cref="IOutbox"/>.</summary>
        public IntegrationEventPublisher(IOutbox outbox)
        {
            _outbox = outbox;
        }

        /// <inheritdoc/>
        public void Publish(IIntegrationEvent integrationEvent)
            => _outbox.Enqueue(IntegrationEventOutboxHandler.HandlerCode, OutboxEnvelope.For(integrationEvent));
    }
}
