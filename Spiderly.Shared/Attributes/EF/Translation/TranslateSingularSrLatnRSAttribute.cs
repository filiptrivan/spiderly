using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Specifies the Serbian Latin singular form translation for a class or property. <br/> <br/>
    /// When applied to a <b>class</b>: <br/>
    /// - Used in base form details UI component title <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// [TranslateSingularSrLatnRS("Korisnički poen")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// </code>
    /// <br/>
    /// When applied to a <b>property</b>: <br/>
    /// - Used as UI field label <br/>
    /// - Used in UI validation messages (e.g., "Polje 'Email adresa' ne može biti prazno") <br/>
    /// - Used in server validation messages (e.g., "Polje 'Email adresa' ne može biti prazno") <br/>
    /// <b>Example:</b> <br/>
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
