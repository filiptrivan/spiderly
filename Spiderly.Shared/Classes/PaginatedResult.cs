using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Classes
{
    public class PaginatedResult<T> where T : class
    {
        public IQueryable<T> Query { get; set; } = null!; // Always set by the generated query pipeline that constructs the result
        public int TotalRecords { get; set; }
    }
}
