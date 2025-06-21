using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies that a property should be included in the generated DTO.
    /// This attribute is particularly useful for enumerable properties, which are not included in DTOs by default. <br/> <br/>
    /// 
    /// <b>Note:</b> This attribute only affects DTO generation and does not influence the mapping behavior (Entity to DTO and vice versa).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IncludeInDTOAttribute : Attribute
    {
    }
}
