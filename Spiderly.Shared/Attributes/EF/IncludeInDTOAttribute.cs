using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies that a property should be included in the generated DTO. <br/>
    /// This attribute is particularly useful for enumerable properties, which are not included in DTOs by default. <br/>
    /// <b>Note:</b> This attribute only affects DTO generation and does not influence the mapping behavior (Entity &lt;-&gt; DTO).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IncludeInDTOAttribute : Attribute
    {
    }
}
