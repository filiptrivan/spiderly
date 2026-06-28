using Spiderly.Shared.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Spiderly.Shared.BaseEntities
{
    /// <summary>
    /// If CRUD operations can be performed on the entity from the application, it should inherit BusinessObject&lt;ID&gt;, if the entity is only for reading from the database (e.g. Gender entity), it should inherit ReadonlyObject&lt;ID&gt;. For BusinessObject entities, the necessary methods for basic CRUD operations will be generated, while e.g. for ReadonlyObject entities Create, Update, Delete methods will not be generated. For ReadonlyObject&lt;T&gt; we don't make CreatedAt and Version properties.
    /// </summary>
    /// <typeparam name="T">Entity's Id type — must be <c>int</c>, <c>long</c>, or <c>byte</c> (enforced by SPIDERLY018 at compile time).</typeparam>
    public class BusinessObject<T> : IBusinessObject<T>, IHasIntegrationEvents where T : struct
    {
        public T Id { get; set; }

        [ConcurrencyCheck]
        [Required]
        public int Version { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        [Required]
        public DateTime ModifiedAt { get; set; }

        private List<IIntegrationEvent> _integrationEvents;

        /// <summary>
        /// Integration events raised on this entity during the current command, awaiting harvest into the
        /// transactional outbox at <c>SaveChanges</c>. Not mapped — never persisted as entity state.
        /// </summary>
        [NotMapped]
        public IReadOnlyCollection<IIntegrationEvent> IntegrationEvents =>
            _integrationEvents ?? (IReadOnlyCollection<IIntegrationEvent>)Array.Empty<IIntegrationEvent>();

        /// <summary>Raises an <see cref="IIntegrationEvent"/> to be delivered after the current transaction commits.</summary>
        /// <param name="integrationEvent">The self-contained event to deliver.</param>
        public void RaiseIntegrationEvent(IIntegrationEvent integrationEvent)
        {
            (_integrationEvents ??= new List<IIntegrationEvent>()).Add(integrationEvent);
        }

        /// <summary>Clears the pending events. Called by the framework after harvesting them to the outbox.</summary>
        public void ClearIntegrationEvents() => _integrationEvents?.Clear();
    }
}
