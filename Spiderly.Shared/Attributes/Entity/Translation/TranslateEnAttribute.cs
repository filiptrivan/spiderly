using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English singular form translation for a class or property. <br/> <br/>
    /// 
    /// <b>When applied to a class:</b> <br/>
    /// - Generates translations for the 'YourClassName' key on both the frontend and backend. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateEn("User point")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// </code>
    /// 
    /// <br/> <br/>
    /// 
    /// <b>When applied to a property:</b> <br/>
    /// - Generates translations for the 'YourClassName' key on both the frontend and backend. <br/>
    /// - Used as the label for an admin form field in the UI. <br/>
    /// - Used in server validation messages (e.g., <i>Field 'Email address' can not be empty"</i>). <br/> 
    /// <br/>
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [TranslateEn("Email address")]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class TranslateEnAttribute : Attribute
    {
        public TranslateEnAttribute(string translate) { }
    }
}
