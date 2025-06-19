using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// <b>Usage:</b> Indicates that the entity represents a helper table for a <i>many-to-many</i> (M2M) relationship. <br/> <br/>
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
