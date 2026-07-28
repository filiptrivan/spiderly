using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.DTO
{
    public class FilterRuleDTO
    {
        public object? Value { get; set; }
        public string MatchMode { get; set; } = null!;
        public string? Operator { get; set; }
    }
}
