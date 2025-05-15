using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Specifies the English plural form translation for a class. <br/> <br/>
    /// <b>This translation is used for:</b> <br/>
    /// - Table titles <br/>
    /// - Excel export filenames (when TranslateExcelEn is not specified) <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// [TranslatePluralEn("User points")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// // Will show as "User points" in table headers
    /// // Will export as "User points.xlsx" if TranslateExcelEn is not specified
    /// </code>
    /// </summary>
    public class TranslatePluralEnAttribute : Attribute
    {
        public TranslatePluralEnAttribute(string translate) { }
    }
}
