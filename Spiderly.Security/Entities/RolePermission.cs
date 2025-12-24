using Spiderly.Shared.Attributes.Entity;

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
