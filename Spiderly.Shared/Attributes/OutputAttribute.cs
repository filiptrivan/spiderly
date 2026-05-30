using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// Supplies a source-generator output location or option for a Spiderly project. This attribute is an
    /// internal configuration hook for generated files and may be replaced by a more explicit configuration
    /// model in a future version.
    /// </summary>
    public class OutputAttribute : Attribute
    {
        /// <param name="output">Output configuration value consumed by the source generator.</param>
        public OutputAttribute(string output) { }
    }
}
