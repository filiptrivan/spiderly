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

        // Parameters/DescendantNodes are populated only on the syntax path (ClassAnalyzer); methods
        // reconstructed from referenced assemblies leave them unset. Left as '= null!' rather than
        // '= new()' so runtime behavior is byte-identical — only syntax-derived methods are consumed
        // by the generators that read these.
        public List<SpiderParameter> Parameters { get; set; } = null!;
        public IEnumerable<SyntaxNode> DescendantNodes { get; set; } = null!;
        public List<SpiderlyAttribute> Attributes { get; set; } = new();

        /// <summary>
        /// Location of the method identifier in source; null for methods reconstructed from
        /// referenced assemblies — callers must fall back to <see cref="Location.None"/>.
        /// </summary>
        public Location? Location { get; set; }
    }
}
