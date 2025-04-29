using Spiderly.Shared.Interfaces;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.BaseEntities
{
    /// <summary>
    /// If CRUD operations can be performed on the entity from the application, it should inherit BusinessObject<ID>, if the entity is only for reading from the database (e.g. Gender entity), it should inherit ReadonlyObject<ID>. For BusinessObject entities, the necessary methods for basic CRUD operations will be generated, while e.g. for ReadonlyObject entities Create, Update, Delete methods will not be generated. For ReadonlyObject<T> we don't make CreatedAt and Version properties.
    /// </summary>
    /// <typeparam name="T">Entity's Id type (long/int/byte)</typeparam>
    public class ReadonlyObject<T> : IReadonlyObject<T> where T : struct
    {
        public T Id { get; protected set; }
    }
}
