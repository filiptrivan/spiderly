using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF.Translation
{
    /// <summary>
    /// Putting it on the class will translate the singular form of it (e.g. for class UserPoint: [TranslateSingularEn("User point")])
    /// It will be used in:
    /// - Base form details UI component 
    /// 
    /// Putting it on the property will translate the singular form of it (e.g. for property EmailAddress: [TranslateSingularEn("Email address")])
    /// It will be used in:
    /// - UI field label
    /// - UI validation (e.g. "Field 'Email address' can not be empty.")
    /// - Server validation (e.g. "Field 'Email address' can not be empty.")
    /// 
    /// </summary>
    public class TranslateSingularEnAttribute : Attribute
    {
        public TranslateSingularEnAttribute(string translate) { }
    }
}
