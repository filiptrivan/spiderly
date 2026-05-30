using System;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Adds email-address validation to the decorated string property. Spiderly emits matching server-side
    /// FluentValidation rules and Angular form validation so the generated API and UI enforce the same email
    /// format requirement.
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [Email] // Must be a valid email address
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EmailAttribute : Attribute
    {
    }
}
