using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Marks a property in a many-to-many relationship where the administration of the relationship
    /// should NOT be performed. This attribute is used in conjunction with M2MMaintanceEntity to
    /// define the relationship management structure. <br/>
    /// Example usage in a many-to-many relationship:
    /// <code>
    /// public class RolePermission
    /// {
    ///     [M2MMaintanceEntity(nameof(Role.Permissions))]
    ///     public virtual Role Role { get; set; }
    /// 
    ///     [M2MEntity(nameof(Permission.Roles))]
    ///     public virtual Permission Permission { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public class M2MEntityAttribute : Attribute // TODO: Consider renaming to better reflect its purpose
    {
        public string WithManyProperty { get; set; }

        /// <param name="withManyProperty">The name of the collection property in the related entity.</param>
        public M2MEntityAttribute(string withManyProperty)
        {
            WithManyProperty = withManyProperty;
        }
    }
}
