using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true)]
    public class CustomValidatorAttribute : Attribute
    {
        public CustomValidatorAttribute(string validationRule)
        {

        }
    }
}
