using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English singular form translation for a class or property. <br/> <br/>
    /// 
    /// <b>When applied to a class:</b>
    /// - Used in base form details UI component title <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateSingularEn("User point")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// </code>
    /// 
    /// <br/> <br/>
    /// 
    /// <b>When applied to a property:</b>
    /// - Used as UI field label <br/>
    /// - Used in server validation messages (e.g., <i>Field 'Email address' can not be empty"</i>) <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [TranslateSingularEn("Email address")]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    public class TranslateSingularEnAttribute : Attribute
    {
        public TranslateSingularEnAttribute(string translate) { }
    }
}
