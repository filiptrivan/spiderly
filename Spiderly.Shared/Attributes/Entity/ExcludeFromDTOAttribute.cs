using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Specifies that a property should be excluded from the generated DTO.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class ExcludeFromDTOAttribute : Attribute
    {
    }
}
