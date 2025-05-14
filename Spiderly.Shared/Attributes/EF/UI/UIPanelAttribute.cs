using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies in which panel the UI control will be located.
    /// By default, all controls are inside the "Details" panel.
    /// </summary>
    public class UIPanelAttribute : Attribute
    {
        /// <param name="panelName">The name of the panel in which the UI control will be placed.</param>
        public UIPanelAttribute(string panelName) { }
    }
}
