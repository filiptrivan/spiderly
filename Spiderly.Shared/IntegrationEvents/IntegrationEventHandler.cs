using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.IntegrationEvents
{
    /// <summary>
    /// Typed convenience base for <see cref="IIntegrationEventHandler"/> — wires <see cref="EventType"/> and the
    /// cast from the non-generic dispatch, so a handler only implements the strongly-typed reaction.
    /// <example>
    /// <code>
    /// public class ReindexOnOrderCreated : IntegrationEventHandler&lt;OrderCreated&gt;
    /// {
    ///     protected override Task HandleAsync(OrderCreated e, CancellationToken ct)
    ///         => _search.ReindexOrderAsync(e.AggregateId, ct);
    /// }
    /// // registered: services.AddScoped&lt;IIntegrationEventHandler, ReindexOnOrderCreated&gt;();
    /// </code>
    /// </example>
    /// </summary>
    /// <typeparam name="TEvent">The integration event type this handler reacts to.</typeparam>
    public abstract class IntegrationEventHandler<TEvent> : IIntegrationEventHandler
        where TEvent : IIntegrationEvent
    {
        /// <inheritdoc/>
        public Type EventType => typeof(TEvent);

        /// <inheritdoc/>
        public virtual string Code => GetType().Name;

        /// <inheritdoc/>
        public Task HandleAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken)
            => HandleAsync((TEvent)integrationEvent, cancellationToken);

        /// <summary>Performs the reaction to the strongly-typed event. Throwing causes the outbox row to be retried.</summary>
        /// <param name="integrationEvent">The rebuilt, strongly-typed event.</param>
        /// <param name="cancellationToken">Cancellation token for the outbox sweep.</param>
        protected abstract Task HandleAsync(TEvent integrationEvent, CancellationToken cancellationToken);
    }
}
