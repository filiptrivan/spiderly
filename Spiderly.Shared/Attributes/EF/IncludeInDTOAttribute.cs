using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Set this attribute to the property you want generated in the DTO.
    /// It only makes sense for enumerable properties(because they are not generated in a DTO by default).
    /// Even if you add this attribute, the property will not be included in the mapping library.
    /// </summary>
    public class IncludeInDTOAttribute : Attribute
    {
    }
}
