using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Defining width of the field on the UI (e.g. UIControlWidth("col-3"))
    /// For full width put col-12
    /// We guess col-12 if you put UIControlTypeCodes: TextArea, Editor
    /// The default value is col-6 md:col-12
    /// </summary>
    public class UIControlWidthAttribute : Attribute
    {
        public UIControlWidthAttribute(string colWidth) { }
    }
}
