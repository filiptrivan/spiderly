namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// A durable, post-commit domain fact ("something happened") delivered to one or more
    /// <see cref="IIntegrationEventHandler"/>s after the transaction that raised it commits. Where an
    /// <see cref="INotification"/> fans out to delivery <i>channels</i> (which render and ship a message), an
    /// integration event fans out to <i>handlers</i> — business reactions that run code.
    ///
    /// <para>An event is raised on an aggregate via <see cref="IHasIntegrationEvents.RaiseIntegrationEvent"/>; the
    /// <c>IntegrationEventOutboxInterceptor</c> harvests it into the transactional outbox in the same transaction,
    /// so it commits atomically with the entity write and rolls back with it. The event is serialized to the outbox
    /// row and rebuilt at delivery, so it must be a self-contained data object — carry ids, not loaded entities.</para>
    ///
    /// <para>Implement this directly for full control, or extend
    /// <see cref="Spiderly.Shared.IntegrationEvents.IntegrationEvent"/> to get the framework-stamped
    /// <c>AggregateId</c>. A type delivered via the outbox must carry a stable
    /// <see cref="Spiderly.Shared.Outbox.OutboxCodeAttribute"/>.</para>
    /// </summary>
    public interface IIntegrationEvent
    {
    }
}
