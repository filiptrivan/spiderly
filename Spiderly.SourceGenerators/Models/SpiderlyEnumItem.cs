using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyEnumItem
    {
        public string Name { get; set; } = null!;

        /// <summary>Null when the member declares no explicit value (<c>Alpha,</c> rather than <c>Alpha = 1,</c>).</summary>
        public string? Value { get; set; }
    }
}
