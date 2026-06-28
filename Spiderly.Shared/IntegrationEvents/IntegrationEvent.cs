using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.IntegrationEvents
{
    /// <summary>
    /// Convenience base for an <see cref="IIntegrationEvent"/> that carries the id of the aggregate it was raised on.
    /// The framework stamps <see cref="AggregateId"/> from the raising entity's (now-assigned) id when it harvests
    /// the event after <c>SaveChanges</c> — so a handler for, say, an <c>OrderCreated</c> event receives the new
    /// order's id without the domain code threading it in. Carry any additional data the handlers need as your own
    /// properties, set when the event is raised from values known before the save.
    /// </summary>
    public abstract class IntegrationEvent : IIntegrationEvent
    {
        /// <summary>
        /// The id of the aggregate this event was raised on, stamped by the framework at harvest time. A value set
        /// explicitly before harvest is preserved (only a default <c>0</c> is overwritten).
        /// </summary>
        public long AggregateId { get; set; }
    }
}
