using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the Serbian Latin form translation for an entity or property. <br/> <br/>
    /// 
    /// <b>When applied to the entity class:</b> <br/>
    /// - Generates translations for the 'YourClassName' key on both the frontend and backend. <br/> <br/>
    /// 
    /// <b>When applied to the property:</b> <br/>
    /// - Generates translations for the 'YourPropertyName' key on both the frontend and backend. <br/>
    /// - Used as the default input field label in the generated Angular form component. <br/>
    /// - Used in both server- and client-side validation messages (e.g., <i>Polje 'Email adresa' ne sme biti prazno"</i>). <br/>
    /// <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateSrLatnRS("Korisnik")]
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [TranslateSrLatnRS("Email adresa")]
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class TranslateSrLatnRSAttribute(string translation) : Attribute
    {
        /// <summary>
        /// Gets the Serbian (Latin) translation for the entity or property.
        /// </summary>
        public string Translation { get; } = translation;
    }
}
