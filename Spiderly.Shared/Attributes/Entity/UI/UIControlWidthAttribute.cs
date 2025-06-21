using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.UI
{
    /// <summary>
    /// <b>Usage:</b> Specifies the width of a UI field using PrimeNG (PrimeFlex) column classes. <br/> <br/>
    /// 
    /// <b>Default values:</b>
    /// - <i>"col-12"</i> for TextArea and Editor controls <br/>
    /// - <i>"col-12 md:col-6"</i> for all other controls <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class Article : &lt;long&gt;
    /// {
    ///     [UIControlWidth("col-12")]
    ///     [UIControlType(nameof(UIControlTypeCodes.TextArea))]
    ///     public string Content { get; set; }
    ///     
    ///     [UIControlWidth("col-3")]
    ///     public string Author { get; set; }
    ///     
    ///     // Uses default "col-12 md:col-6"
    ///     public string Title { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UIControlWidthAttribute : Attribute
    {
        public UIControlWidthAttribute(string colWidth) { }
    }
}
