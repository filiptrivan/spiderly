using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Specifies a custom validation rule to be applied to the decorated property. <br/>
    /// Multiple validation rules can be chained together using dot notation e.g. <i>EmailAddress().Length(5, 10)</i> <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// // Class Example
    /// [CustomValidator("RuleFor(x => x.Email).EmailAddress().Length(5, 50);")] // Validates email format and length
    /// [CustomValidator("RuleFor(x => x.Name).Length(2, 100);")] // Validates name length
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     public string Email { get; set; }
    ///     
    ///     public string Name { get; set; }
    /// }
    /// 
    /// // Property Example
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [CustomValidator("EmailAddress().Length(5, 50)")] // Validates email format and length
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class, AllowMultiple = true)]
    public class CustomValidatorAttribute : Attribute
    {
        public CustomValidatorAttribute(string validationRule)
        {

        }
    }
}
