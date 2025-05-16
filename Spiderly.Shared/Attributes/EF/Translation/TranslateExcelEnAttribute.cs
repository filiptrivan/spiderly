using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English name for the exported Excel file. <br/> <br/>
    /// 
    /// <b>If not specified:</b>
    /// - First tries to use <i>TranslatePluralEn</i> value <br/>
    /// - If <i>TranslatePluralEn</i> is not available, uses <i>'{class_name}List'</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
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
