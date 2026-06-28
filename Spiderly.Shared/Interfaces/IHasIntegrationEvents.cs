namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Implemented by entities that can raise <see cref="IIntegrationEvent"/>s. The Spiderly entity base
    /// <c>BusinessObject&lt;T&gt;</c> implements this, so a created or updated entity can raise events; the
    /// <c>IntegrationEventOutboxInterceptor</c> scans tracked entities for pending events after each save.
    ///
    /// <para>Events accumulate on the entity during a command and are harvested into the transactional outbox at
    /// <c>SaveChanges</c> time (after the entity's id is assigned), then cleared. Raising an event is part of the
    /// domain operation, so the fact cannot be forgotten by a caller that skips a manual publish.</para>
    ///
    /// <para><b>Created/updated aggregates only.</b> The harvest runs <i>after</i> the save, when EF Core has already
    /// detached deleted entities — so an event raised on an entity that is being <c>Remove</c>d is invisible to the
    /// scan and would be silently dropped. Publish delete-time facts (and any fact with no owning aggregate write)
    /// explicitly via <see cref="IIntegrationEventPublisher"/> inside the same <c>WithTransactionAsync</c> instead.</para>
    /// </summary>
    public interface IHasIntegrationEvents
    {
        /// <summary>The events raised on this entity and not yet harvested. Empty when nothing has been raised.</summary>
        IReadOnlyCollection<IIntegrationEvent> IntegrationEvents { get; }

        /// <summary>Raises an event to be delivered after the current transaction commits.</summary>
        /// <param name="integrationEvent">The self-contained event to deliver.</param>
        void RaiseIntegrationEvent(IIntegrationEvent integrationEvent);

        /// <summary>Clears the pending events. Called by the framework after harvesting them to the outbox.</summary>
        void ClearIntegrationEvents();
    }
}
