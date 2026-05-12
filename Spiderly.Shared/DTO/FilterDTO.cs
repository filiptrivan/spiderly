using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class FilterDTO
    {
        public Dictionary<string, List<FilterRuleDTO>> Filters { get; set; } = new();
        public int First { get; set; }
        public int Rows { get; set; }
        public List<FilterSortMetaDTO> MultiSortMeta { get; set; } = new();
        public int? AdditionalFilterIdInt { get; set; }
        public long? AdditionalFilterIdLong { get; set; }
        public byte? AdditionalFilterIdByte { get; set; }
    }
}
