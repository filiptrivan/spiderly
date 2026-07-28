using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class PaginatedResultDTO<T> where T : class
    {
        public IList<T> Data { get; set; } = new List<T>();
        public int TotalRecords { get; set; }
    }
}
