using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.FluentValidation
{

    /// <summary>
    /// If you want to add more custom validators here, you need to change the generator also
    /// Generator only support property validators, not the whole DTO ones (eg. x => x, only x => x.Name)
    /// </summary>
    public static class CustomValidators
    {
        public static bool NotHaveWhiteSpace(string input)
        {
            return !input.Any(x => char.IsWhiteSpace(x));
        }
    }
}
