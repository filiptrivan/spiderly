using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderParameter
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
