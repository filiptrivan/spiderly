using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// Specifies a custom validation rule to be applied to the decorated property. <br/>
    /// Multiple validation rules can be chained together using dot notation e.g. <b>EmailAddress().Length(5, 10)</b> <br/> <br/>
    /// <b>Example:</b> <br/>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [CustomValidator("EmailAddress().Length(5, 50)")] // Validates email format and length
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class CustomValidatorAttribute : Attribute
    {
        public CustomValidatorAttribute(string validationRule)
        {

        }
    }
}
