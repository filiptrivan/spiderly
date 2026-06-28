namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Stages an <see cref="IIntegrationEvent"/> to the transactional outbox <b>explicitly</b> — for facts not tied to an
    /// aggregate write (an inbound webhook, a scheduled job, a security event), where the harvest interceptor has no
    /// tracked entity to raise the event on. Call inside a <c>WithTransactionAsync</c> block (or before a
    /// <c>SaveChanges</c>) so the row is staged on the current scoped <see cref="IApplicationDbContext"/>.
    ///
    /// <para>This and the aggregate-raise harvest produce the <b>same</b> outbox row, delivered by the same
    /// <c>IntegrationEventOutboxHandler</c> — handlers neither know nor care which trigger staged the event.</para>
    /// </summary>
    public interface IIntegrationEventPublisher
    {
        /// <summary>Stages the event for post-commit delivery via the outbox.</summary>
        /// <param name="integrationEvent">The self-contained event to deliver.</param>
        void Publish(IIntegrationEvent integrationEvent);
    }
}
