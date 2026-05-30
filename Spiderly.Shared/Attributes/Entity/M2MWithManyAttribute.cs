using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.Entity
{
    /// <summary>
    /// Identifies one side of a many-to-many junction entity and names the inverse collection on the related
    /// entity. Use it on the two reference navigation properties inside a class marked with
    /// <see cref="M2MAttribute"/>.
    /// <br/> <br/>
    /// 
    /// <b>Example:</b>
    /// <code>
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
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)] // TODO: Make Roslyn analyzer to check if there is more than two attributes on the properties in the class.
    public class M2MWithManyAttribute : Attribute
    {
        public string WithManyProperty { get; set; }

        /// <param name="withManyProperty">The collection navigation property name on the related entity.</param>
        public M2MWithManyAttribute(string withManyProperty)
        {
            WithManyProperty = withManyProperty;
        }
    }
}
