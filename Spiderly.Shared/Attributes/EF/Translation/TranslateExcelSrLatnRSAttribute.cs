using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Specifies the Serbian Latin name for the exported Excel file. <br/> <br/>
    /// Used to customize the Excel export filename. <br/>
    /// <b>If not specified:</b> <br/>
    /// - First tries to use TranslatePluralSrLatnRS value <br/>
    /// - If TranslatePluralSrLatnRS is not available, uses '{class_name}List' <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// [TranslateExcelSrLatnRS("Korisnici_Excel")]
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// // Will generate: Korisnici_Excel.xlsx
    /// </code>
    /// </summary>
    public class TranslateExcelSrLatnRSAttribute : Attribute
    {
        public TranslateExcelSrLatnRSAttribute(string translate) { }
    }
}
