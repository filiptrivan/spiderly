using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyProperty
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public string StringValue { get; set; } // public string Name { get; set; } = "Filip" --> "Filip"
        public string EntityName { get; set; } // TODO FT: Add to every case, you didn't finished this, but it works for now.
        public bool IsSaveBodyMainDTO { get; set; }

        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
