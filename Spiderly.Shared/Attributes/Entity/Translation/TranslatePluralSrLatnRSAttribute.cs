using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the Serbian Latin plural form translation for an entity. <br/> <br/>
    /// 
    /// <b>This translation is used for:</b> <br/>
    /// - Generates translations for the 'YourEntityNameList' key on both the frontend and backend. <br/>
    /// - Table titles (used by default when generating pages with the 'add-new-entity' Spiderly command; this can be customized). <br/>
    /// - Excel export filenames (when <i>TranslateExcelSrLatnRS</i> is not specified) <br/>
    /// <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslatePluralSrLatnRS("Korisnički poeni")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Entity properties
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TranslatePluralSrLatnRSAttribute(string translation) : Attribute
    {
        /// <summary>
        /// Gets the Serbian (Latin) plural translation for the entity.
        /// </summary>
        public string Translation { get; } = translation;
    }
}
