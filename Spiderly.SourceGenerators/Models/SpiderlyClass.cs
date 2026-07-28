using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyClass
    {
        public string Name { get; set; } = null!;
        public string Namespace { get; set; } = null!;

        /// <summary>
        /// Location of the class identifier in source, used to anchor Roslyn diagnostics.
        /// Null for classes reconstructed from referenced assemblies — callers must fall back to <see cref="Location.None"/>.
        /// </summary>
        public Location? Location { get; set; }

        /// <summary>
        /// Here is only one base type, no interfaces. Null when the class declares no base type.
        /// </summary>
        public string? BaseType { get; set; }

        public bool IsAbstract { get; set; }

        /// <summary>Set only for controller classes; null otherwise.</summary>
        public string? ControllerName { get; set; }

        /// <summary>
        /// For the DTO classes
        /// </summary>
        public bool IsGenerated { get; set; }

        /// <summary>The class's XML doc summary; null when it carries none.</summary>
        public string? Description { get; set; }

        public List<SpiderlyProperty> Properties { get; set; } = new();

        public List<SpiderlyAttribute> Attributes { get; set; } = new();

        public List<SpiderlyMethod> Methods { get; set; } = new();
    }
}
