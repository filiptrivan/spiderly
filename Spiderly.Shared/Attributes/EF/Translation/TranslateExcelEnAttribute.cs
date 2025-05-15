using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Specifies the English name for the exported Excel file. <br/> <br/>
    /// Used to customize the Excel export filename. <br/>
    /// <b>If not specified:</b> <br/>
    /// - First tries to use TranslatePluralEn value <br/>
    /// - If TranslatePluralEn is not available, uses '{class_name}List' <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// [TranslateExcelEn("Users_Excel")]
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     // Class properties
    /// }
    /// // Will generate: Users_Excel.xlsx
    /// </code>
    /// </summary>
    public class TranslateExcelEnAttribute : Attribute
    {
        public TranslateExcelEnAttribute(string translate) { }
    }
}
