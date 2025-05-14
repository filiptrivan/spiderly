using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// With this control, you determine the order in which controls will be displayed on the UI.
    /// The controls are displayed in the order you specified the properties on the entity (except `file`, `text-area`, `editor`, `table` control types, they are always displayed last in the written order).
    /// </summary>
    public class UIPropertyBlockOrderAttribute : Attribute // TODO FT: Make this attribute work
    {
        public UIPropertyBlockOrderAttribute(string orderNumber) { }
    }
}
