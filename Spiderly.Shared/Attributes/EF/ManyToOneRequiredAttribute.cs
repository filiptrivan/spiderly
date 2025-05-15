using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Specifies that a many-to-one relationship property is required and should trigger cascade delete.
    /// This attribute generates both backend and frontend validation rules to ensure the relationship
    /// is always present.
    /// </summary>
    /// <remarks>
    /// When applied to a property:
    /// - Enforces that the parent entity cannot exist without this relationship
    /// - Implements cascade delete behavior
    /// - Generates required validation rules for both backend and frontend
    /// 
    /// Example usage:
    /// <code>
    /// public class User : BusinessObject&lt;long&gt;
    /// {
    ///     [ManyToOneRequired]
    ///     [WithMany(nameof(Gender.Users))]
    ///     public virtual Gender Gender { get; set; }
    /// }
    /// </code>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property)]
    public class ManyToOneRequiredAttribute : Attribute
    {
    }
}
