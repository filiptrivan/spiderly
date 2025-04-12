using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyProperty
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string EntityName { get; set; } // TODO FT: Add to every case, you didn't finished this, but it works for now.

        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
