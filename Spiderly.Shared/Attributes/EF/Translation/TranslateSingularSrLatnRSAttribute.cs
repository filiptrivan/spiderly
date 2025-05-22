using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the Serbian Latin singular form translation for a class or property. <br/> <br/>
    /// 
    /// <b>When applied to a class:</b>
    /// - Used in base form details UI component title <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateSingularSrLatnRS("Korisnički poen")]
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
    /// - Used in server validation messages (e.g., <i>Polje 'Email adresa' ne sme biti prazno"</i>) <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [TranslateSingularSrLatnRS("Email adresa")]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    public class TranslateSingularSrLatnRSAttribute : Attribute
    {
        public TranslateSingularSrLatnRSAttribute(string translate) { }
    }
}
