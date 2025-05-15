using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.UI
{
    /// <summary>
    /// Specifies the display order of UI controls. <br/> <br/>
    /// Controls are displayed in the order of property declaration, except for 'file', 'text-area', 'editor', and 'table' controls, 
    /// which are always displayed last in their declaration order. <br/> <br/>
    /// <b>Example:</b> <br/>
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
