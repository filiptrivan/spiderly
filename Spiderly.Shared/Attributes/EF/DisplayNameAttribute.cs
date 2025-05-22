using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b>
    /// Specifies which property should be used as the display name for an entity in UI elements: <br/>
    /// - When applied to a property: The property's value will be used to represent the entity <br/>
    /// - When applied to a class: The specified property's value will be used to represent the entity <br/>
    /// - If no property or class is marked with this attribute: The entity's 'Id' will be used <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [DisplayName]
    ///     public string FullName { get; set; } // Will be used in dropdowns and lists
    /// }
    /// 
    /// [DisplayName("Department.Name")] // Uses related entity's property
    /// public class Employee : BusinessObject&lt;long&gt;
    /// {
    ///     public virtual Department Department { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public class DisplayNameAttribute : Attribute
    {
        /// <param name="displayName">Optional. The fully qualified property path (e.g., "User.Email"). 
        /// Only needed when the display name property is in a related entity. <br/>
        /// <b>WARNING:</b> Don't use nameof(User.Email) here, use the simple string 
        /// "User.Email" instead.</param>
        public DisplayNameAttribute(string displayName = null)
        {
        }
    }
}
