using Spiderly.Shared.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Spiderly.Shared.BaseEntities
{
    /// <summary>
    /// If CRUD operations can be performed on the entity from the application, it should inherit BusinessObject<ID>, if the entity is only for reading from the database (e.g. Gender entity), it should inherit ReadonlyObject<ID>. For BusinessObject entities, the necessary methods for basic CRUD operations will be generated, while e.g. for ReadonlyObject entities Create, Update, Delete methods will not be generated. For ReadonlyObject<T> we don't make CreatedAt and Version properties.
    /// </summary>
    /// <typeparam name="T">Entity's Id type (long/int/byte)</typeparam>
    public class BusinessObject<T> : IBusinessObject<T> where T : struct
    {
        public T Id { get; private set; } // FT: Protected doesn't work with Mappster

        [ConcurrencyCheck]
        [Required]
        public int Version { get; private set; }

        [Required]
        public DateTime CreatedAt { get; private set; }

        [Required]
        public DateTime ModifiedAt { get; private set; }

        public void SetVersion(int version)
        {
            Version = version;
        }

        public void SetCreatedAt(DateTime createdAt)
        {
            CreatedAt = createdAt;
        }

        public void SetModifiedAt(DateTime modifiedAt)
        {
            ModifiedAt = modifiedAt;
        }
    }
}
