using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.BaseEntities
{
    /// <summary>
    /// If CRUD operations can be performed on the entity from the application, it should inherit BusinessObject&lt;ID&gt;, if the entity is only for reading from the database (e.g. Gender entity), it should inherit ReadonlyObject&lt;ID&gt;. For BusinessObject entities, the necessary methods for basic CRUD operations will be generated, while e.g. for ReadonlyObject entities Create, Update, Delete methods will not be generated. For ReadonlyObject&lt;T&gt; we don't make CreatedAt and Version properties.
    /// Id is not protected here because in most cases we want to assign it inside EF Core infrastructure project manually.
    /// </summary>
    /// <typeparam name="T">Entity's Id type (long/int/byte)</typeparam>
    public class ReadonlyObject<T> : IReadonlyObject<T> where T : struct
    {
        public T Id { get; set; }
    }
}
