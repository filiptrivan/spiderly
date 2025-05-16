using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Specifies in which panel the UI control will be located.
    /// By default, all controls are inside the <i>"Details"</i> panel. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string Name { get; set; } // Goes to "Details" panel by default
    ///     
    ///     [UIPanel("Security")]
    ///     public string Password { get; set; } // Goes to "Security" panel
    ///     
    ///     [UIPanel("Preferences")]
    ///     public bool ReceiveNotifications { get; set; } // Goes to "Preferences" panel
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UIPanelAttribute : Attribute // TODO: Make this attribute work
    {
        /// <param name="panelName">The name of the panel in which the UI control will be placed.</param>
        public UIPanelAttribute(string panelName) { }
    }
}
