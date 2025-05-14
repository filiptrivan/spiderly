using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Shared.Attributes.EF
{
    /// <summary>
    /// Put this attribute to the property in many to many relationship, on which page you want to do administration
    /// of many to many.
    /// e.g. Here the multiautocomplete for M2M administration will be on Role page:
    /// public class RolePermission
    /// {
    ///     [M2MMaintanceEntity(nameof(Role.Permissions))]
    ///     public virtual Role Role { get; set; }
    /// 
    ///     [M2MEntity(nameof(Permission.Roles))]
    ///     public virtual Permission Permission { get; set; }
    /// }
    /// </summary>
    public class M2MMaintanceEntityAttribute : Attribute
    {
        public string WithManyProperty { get; set; }

        public M2MMaintanceEntityAttribute(string withManyProperty)
        {
            WithManyProperty = withManyProperty;
        }
    }
}
