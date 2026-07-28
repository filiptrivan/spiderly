using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class FilterSortMetaDTO
    {
        public string Field { get; set; } = null!;
        public int Order { get; set; }
    }
}
