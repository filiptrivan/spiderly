using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Forces the decorated property to be included in generated DTO classes. This is mainly useful for
    /// enumerable or otherwise skipped properties that Spiderly does not include in DTOs by default.
    /// <br/> <br/>
    /// 
    /// <b>Note:</b> This attribute only affects DTO generation and does not influence the mapping behavior (Entity to DTO and vice versa).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class IncludeInDTOAttribute : Attribute
    {
    }
}
