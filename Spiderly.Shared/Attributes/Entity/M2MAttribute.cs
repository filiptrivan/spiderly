using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Marks an entity as the junction/helper table for a many-to-many relationship. Decorated classes are
    /// interpreted by Spiderly relationship generation as linking two aggregate entities through their
    /// <see cref="M2MWithManyAttribute"/> navigation properties.
    /// <br/> <br/>
    /// <b>Example:</b>
    /// <code>
    /// [M2M]
    /// public class RolePermission
    /// {
    ///     [M2MWithMany(nameof(Role.Permissions))]
    ///     public virtual Role Role { get; set; }
    /// 
    ///     [M2MWithMany(nameof(Permission.Roles))]
    ///     public virtual Permission Permission { get; set; }
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class M2MAttribute : Attribute
    {
    }
}
