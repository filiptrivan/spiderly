using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// <b>Usage:</b> Apply to a property to exclude it from the UI form.
    /// Apply to an entity to exclude the entire UI form generation. 
    /// Apply to a controller method to exclude the UI controller method generation. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     public string Name { get; set; }
    ///     
    ///     [UIDoNotGenerate]
    ///     public DateTime LastLoginDate { get; set; } // Won't appear in the UI form
    ///     
    ///     [UIDoNotGenerate]
    ///     public string InternalNotes { get; set; } // Won't appear in the UI form
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class | AttributeTargets.Method)]
    public class UIDoNotGenerateAttribute : Attribute
    {
    }
}
