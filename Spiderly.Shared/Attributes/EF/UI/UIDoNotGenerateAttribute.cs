using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// If you put this attribute on the property the field will not generate on the UI main form component
    /// </summary>
    public class UIDoNotGenerateAttribute : Attribute
    {
    }
}
