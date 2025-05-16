using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Validates that a numeric property value is greater than or equal to a specified number.
    /// This attribute provides both <i>server-side</i> and <i>client-side</i> validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class GreaterThanOrEqualToAttribute : Attribute
    {
        /// <param name="number">The minimum allowed value. The property value must be greater than or equal to this number.</param>
        public GreaterThanOrEqualToAttribute(int number) { }
    }
}
