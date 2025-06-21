using Spiderly.Shared.Attributes.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Spiderly.Security.Entities
{
    [M2M]
    public class RolePermission
    {
        [M2MWithMany(nameof(Role.Permissions))]
        public virtual Role Role { get; set; }

        [M2MWithMany(nameof(Permission.Roles))]
        public virtual Permission Permission { get; set; }
    }
}
