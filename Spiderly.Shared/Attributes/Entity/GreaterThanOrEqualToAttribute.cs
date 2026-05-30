using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Adds a minimum numeric value rule to the decorated property. Spiderly emits matching server-side
    /// FluentValidation rules and Angular form validation so the generated API and UI both require the value
    /// to be greater than or equal to the configured number.
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class Product : BusinessObject&lt;long&gt;
    /// {
    ///     [GreaterThanOrEqualTo(0)] // StockQuantity must be 0 or higher
    ///     public int StockQuantity { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class GreaterThanOrEqualToAttribute : Attribute
    {
        /// <param name="number">The inclusive minimum value allowed for the decorated numeric property.</param>
        public GreaterThanOrEqualToAttribute(int number) { }
    }
}
