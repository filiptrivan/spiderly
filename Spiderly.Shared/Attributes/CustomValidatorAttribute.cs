using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Defines custom validation rules for the decorated property or class (can be used on DTOs as well as entities). <br/>
    /// Multiple rules can be chained using dot notation, e.g., <i>EmailAddress().Length(5, 50)</i>. <br/><br/>
    ///
    /// <b>Examples:</b>
    /// <code>
    /// // Class-level validation
    /// [CustomValidator("RuleFor(x => x.Email).EmailAddress().Length(5, 50);")]
    /// [CustomValidator("RuleFor(x => x.Name).Length(2, 100);")]
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     public string Email { get; set; }
    ///     public string Name { get; set; }
    /// }
    ///
    /// // Property-level validation
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [CustomValidator("EmailAddress()")]
    ///     [CustomValidator("Length(5, 50)")]
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
