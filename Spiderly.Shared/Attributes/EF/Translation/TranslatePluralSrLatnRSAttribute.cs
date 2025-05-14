using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Putting it on the class you are translating the plural form of it. <br/>
    /// The translation will be used in:
    /// - Table title
    /// - Posibly excel export if it's not provided 
    /// </summary>
    public class TranslatePluralSrLatnRSAttribute : Attribute
    {
        public TranslatePluralSrLatnRSAttribute(string translate) { }
    }
}
