using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Generates a string property in the DTO containing display names for an enumerable collection.
    /// This attribute facilitates the presentation of collection items as comma-separated display names in UI tables.
    /// </summary>
    /// <remarks>
    /// The generated property will be populated with display names
    /// of the collection items using the Mapster.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class GenerateCommaSeparatedDisplayNameAttribute : Attribute
    {
        public GenerateCommaSeparatedDisplayNameAttribute() { }
    }
}
