using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the Serbian Latin name for the exported Excel file. <br/> <br/>
    /// 
    /// <b>If not specified:</b> <br/>
    /// - First tries to use <i>TranslatePluralSrLatnRS</i> value. <br/>
    /// - If <i>TranslatePluralSrLatnRS</i> is not available, uses <i>'YourEntityNameList'</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateExcelSrLatnRS("Korisnici_Excel")] // Will generate: Korisnici_Excel.xlsx
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     // Entity properties
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TranslateExcelSrLatnRSAttribute : Attribute
    {
        public TranslateExcelSrLatnRSAttribute(string translate) { }
    }
}
