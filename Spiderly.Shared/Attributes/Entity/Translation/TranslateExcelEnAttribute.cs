using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity.Translation
{
    /// <summary>
    /// <b>Usage:</b> Specifies the English name for the exported Excel file. <br/> <br/>
    /// 
    /// <b>If not specified:</b> <br/>
    /// - First tries to use <i>TranslatePluralEn</i> value. <br/>
    /// - If <i>TranslatePluralEn</i> is not available, uses <i>'YourEntityNameList'</i>. <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// [TranslateExcelEn("Users_Excel")] // Will generate: Users_Excel.xlsx
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     // Entity properties
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class TranslateExcelEnAttribute : Attribute
    {
        public TranslateExcelEnAttribute(string translate) { }
    }
}
