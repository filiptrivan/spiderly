using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.UI
{
    /// <summary>
    /// <b>Usage:</b> Specifies the display order of UI controls.
    /// Controls are displayed in the order of property declaration, except for: 
    /// <i>'file'</i>, <i>'text-area'</i>, <i>'editor'</i>, and <i>'table'</i> controls, 
    /// which are always displayed last in their declaration order. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class Article : BusinessObject&lt;long&gt;
    /// {
    ///     [UIPropertyBlockOrder("1")]
    ///     public string Title { get; set; }
    ///     
    ///     [UIPropertyBlockOrder("2")]
    ///     public string Author { get; set; }
    ///     
    ///     // Will be displayed last despite order number
    ///     [UIPropertyBlockOrder("0")]
    ///     [UIControlType(nameof(UIControlTypeCodes.TextArea))]
    ///     public string Content { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class UIPropertyBlockOrderAttribute : Attribute // TODO: Make this attribute work
    {
        public UIPropertyBlockOrderAttribute(string orderNumber) { }
    }
}
