using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyMethod
    {
        public string Name { get; set; } = null!;
        public string ReturnType { get; set; } = null!;

        /// <summary>Null for a bodiless method (abstract / expression-bodied-less declarations).</summary>
        public string? Body { get; set; }

        /// <summary>
        /// Populated only on the syntax path (<c>ClassAnalyzer</c>); methods reconstructed from referenced
        /// assemblies leave it null, and <c>ReferencedSpiderlyClassListComparer</c> compares it in that
        /// state — so the null is real, not a placeholder.
        /// </summary>
        public List<SpiderParameter>? Parameters { get; set; }

        public List<SpiderlyAttribute> Attributes { get; set; } = new();

        /// <summary>
        /// Location of the method identifier in source; null for methods reconstructed from
        /// referenced assemblies — callers must fall back to <see cref="Location.None"/>.
        /// </summary>
        public Location? Location { get; set; }
    }
}
