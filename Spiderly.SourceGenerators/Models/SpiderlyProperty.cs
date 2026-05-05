using Microsoft.CodeAnalysis;
using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyProperty
    {
        public string Type { get; set; }
        public string Name { get; set; }

        /// <summary>
        /// Location of the property identifier in source, used to anchor Roslyn diagnostics.
        /// Null for synthesized properties (DTO-generated, base-class stubs).
        /// </summary>
        public Location Location { get; set; }

        /// <summary>
        /// input: public string Name { get; set; } = "Filip"
        /// output: "Filip"
        /// </summary>
        public string StringValue { get; set; }

        public string EntityName { get; set; } // TODO FT: Add to every case, you didn't finished this, but it works for now.
        public bool IsSaveBodyMainDTO { get; set; }

        public string Description { get; set; }

        /// <summary>
        /// True when this property's type is a C# enum decorated with <c>[SpiderlyEnum]</c>.
        /// Set during class analysis when an enum-name set is supplied; defaults to false otherwise.
        /// Generators consult this to short-circuit M2O classification (an enum is a scalar value, not a navigation property).
        /// </summary>
        public bool IsEnum { get; set; }

        public List<SpiderlyAttribute> Attributes { get; set; } = new();
    }
}
