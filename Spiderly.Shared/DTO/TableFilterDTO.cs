using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class TableFilterDTO
    {
        public Dictionary<string, List<TableFilterContext>> Filters { get; set; } = new();
        public int First { get; set; }
        public int Rows { get; set; }
        public List<TableFilterSortMeta> MultiSortMeta { get; set; } = new();
        public int? AdditionalFilterIdInt { get; set; }
        public long? AdditionalFilterIdLong { get; set; }
    }
}
