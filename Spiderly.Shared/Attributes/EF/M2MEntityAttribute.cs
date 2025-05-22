using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Marks a property in a <i>many-to-many</i> relationship where the administration of the relationship
    /// should NOT be performed. This attribute is used in conjunction with <i>M2MMaintanceEntity</i> to
    /// define the relationship management structure. <br/> <br/>
    /// 
    /// <b>Example:</b>
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
