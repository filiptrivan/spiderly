using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderParameter
    {
        public string Name { get; set; } = null!;
        public SpiderlyTypeRef Type { get; set; } = null!;
        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
