using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Putting it on the class translates the exported Excel file name (e.g. [TranslateExcelSrLatn("Korisnici_Excel")] -> Korisnici_Excel.xlsx) <br/>
    /// If not provided we will try to get the value from TranslatePluralSr, if neither that value is provided we will use `{class_name}List` name
    /// </summary>
    public class TranslateExcelSrLatnRSAttribute : Attribute
    {
        public TranslateExcelSrLatnRSAttribute(string translate) { }
    }
}
