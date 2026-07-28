using System;
using System.Collections.Generic;
using System.Text;

namespace Spiderly.SourceGenerators.Models
{
    public class SpiderlyAttribute
    {
        public string Name { get; set; } = null!;

        /// <summary>
        /// Doesn't handle if more values are in the prenteces, eg. [Attribute("First", "Second")]
        /// <para>Null for a bare attribute with no argument list (e.g. <c>[Required]</c>).</para>
        /// </summary>
        public string? Value { get; set; }
    }
}
