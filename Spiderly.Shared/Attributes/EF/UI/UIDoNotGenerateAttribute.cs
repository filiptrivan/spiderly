using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Excludes a property from being generated in the UI main form component. <br/> <br/>
    /// <b>Example:</b> <br/>
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
    [AttributeUsage(AttributeTargets.Property)]
    public class UIDoNotGenerateAttribute : Attribute
    {
    }
}
