using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English form translation for an entity or property. <br/> <br/>
    /// 
    /// <b>When applied to the entity class:</b> <br/>
    /// - Generates translations for the 'YourEntityName' key on both the frontend and backend. <br/> 
    /// <br/>
    /// 
    /// <b>When applied to the property:</b> <br/>
    /// - Generates translations for the 'YourPropertyName' key on both the frontend and backend. <br/>
    /// - Used as the default input field label in the generated Angular form component. <br/>
    /// - Used in both server- and client-side validation messages (e.g., <i>Field 'Email address' can not be empty"</i>). <br/> 
    /// <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateEn("User")]
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [TranslateEn("Email address")]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class TranslateEnAttribute(string translation) : Attribute
    {
        /// <summary>
        /// Gets the English translation for the entity or property.
        /// </summary>
        public string Translation { get; } = translation;
    }
}
