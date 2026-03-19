using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// <b>Usage:</b> Validates that a string property value is a valid email address.
    /// This attribute provides both <i>server-side</i> and <i>client-side</i> validation.
    /// <br/><br/>
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [EmailAddress] // Must be a valid email address
    ///     public string Email { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class EmailAddressAttribute : Attribute
    {
    }
}
