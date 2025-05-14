using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    public class DisplayNameAttribute : Attribute
    {
        /// <summary>
        /// A Property with this attribute will be used as a display name for the class it is in (e.g. when we display the 
        /// UserExtended list in the dropdown, their emails will be used for display). If you don't put this property anywhere, 
        /// the Id would be used for display name.
        /// Don't use nameof to define display name, because source generator will take only "Email" 
        /// if you pass nameof(User.Email)
        /// </summary>
        /// <param name="displayName">Pass this parameter only if the display name is like this: User.Email</param>
        public DisplayNameAttribute(string displayName = null)
        {

        }
    }
}
