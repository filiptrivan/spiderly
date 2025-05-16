using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the Serbian Latin name for the exported Excel file. <br/> <br/>
    /// 
    /// <b>If not specified:</b>
    /// - First tries to use <i>TranslatePluralSrLatnRS</i> value <br/>
    /// - If <i>TranslatePluralSrLatnRS</i> is not available, uses <i>'{class_name}List'</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
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
