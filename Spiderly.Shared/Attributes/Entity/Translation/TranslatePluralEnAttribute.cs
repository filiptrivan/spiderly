using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English plural form translation for a class. <br/> <br/>
    /// 
    /// <b>This translation is used for:</b> <br/>
    /// - Generates translations for the 'YourClassNameList' key on both the frontend and backend. <br/>
    /// - Table titles (used by default when generating pages with the 'add-new-page' Spiderly command; this can be customized). <br/>
    /// - Excel export filenames (when <i>TranslateExcelEn</i> is not specified). <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslatePluralEn("User points")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Entity properties
    /// }
    /// // Will show as "User points" in table headers
    /// // Will export as "User points.xlsx" if TranslateExcelEn is not specified
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TranslatePluralEnAttribute : Attribute
    {
        public TranslatePluralEnAttribute(string translate) { }
    }
}
