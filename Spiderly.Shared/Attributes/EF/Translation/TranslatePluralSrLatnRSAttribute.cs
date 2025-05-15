using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Specifies the Serbian Latin plural form translation for a class. <br/> <br/>
    /// <b>This translation is used for:</b> <br/>
    /// - Table titles <br/>
    /// - Excel export filenames (when TranslateExcelSrLatnRS is not specified) <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// [TranslatePluralSrLatnRS("Korisnički poeni")]
    /// public class UserPoint : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// // Will show as "Korisnički poeni" in table headers
    /// // Will export as "Korisnički poeni.xlsx" if TranslateExcelSrLatnRS is not specified
    /// </code>
    /// </summary>
    public class TranslatePluralSrLatnRSAttribute : Attribute
    {
        public TranslatePluralSrLatnRSAttribute(string translate) { }
    }
}
