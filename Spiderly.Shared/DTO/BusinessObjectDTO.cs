using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.DTO
{
    public class BusinessObjectDTO<T> : IBusinessObjectDTO<T>
    {
        public T Id { get; set; }
        public int? Version { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ModifiedAt { get; set; }
    }
}
