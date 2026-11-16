using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The entity classes of <c>Backend/{AppName}.Business/Entities/</c>: the security entities and the outbox row.
    /// </summary>
    internal static class EntitiesTemplate
    {
        internal static List<SpiderlyFile> Files(string appName) => new()
        {
            new SpiderlyFile { Name = "User.cs", Data = GetUserCsData(appName) },
            new SpiderlyFile { Name = "Role.cs", Data = GetRoleCsData(appName) },
            new SpiderlyFile { Name = "Permission.cs", Data = GetPermissionCsData(appName) },
            new SpiderlyFile { Name = "RolePermission.cs", Data = GetRolePermissionCsData(appName) },
            new SpiderlyFile { Name = "UserRole.cs", Data = GetUserRoleCsData(appName) },
            new SpiderlyFile { Name = "UserExternalLogin.cs", Data = GetUserExternalLoginCsData(appName) },
            new SpiderlyFile { Name = "OutboxMessage.cs", Data = GetOutboxMessageCsData(appName) },
        };

        private static string GetUserCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Email), IsUnique = true)]
    [SpiderlyEntity]
    public class User : BusinessObject<long>, IUser
    {
        [UIDoNotGenerate]
        [DisplayName]
        [Email]
        [StringLength(70, MinimumLength = 5)]
        [Required]
        public string Email { get; set; } = null!;

        public bool? IsDisabled { get; set; }

        public virtual List<Role> Roles { get; } = new(); // M2M
        IReadOnlyCollection<IRole> ISecurityPrincipal.Roles => Roles; // Roles moved to the principal base

        [UIDoNotGenerate]
        public virtual List<UserExternalLogin> ExternalLogins { get; } = new();
    }
}
""";
        }

        private static string GetUserExternalLoginCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    // Links a user to an external identity-provider login, keyed by the provider's stable subject.
    // [UIDoNotGenerate]: auth plumbing, not admin-editable. A non-nullable FK (UserId) requires
    // [Required] on the navigation, otherwise the SPIDERLY006 diagnostic fires.
    [Index(nameof(Provider), nameof(ProviderKey), IsUnique = true)]
    [UIDoNotGenerate]
    [SpiderlyEntity]
    public class UserExternalLogin : BusinessObject<long>, IUserExternalLogin
    {
        [Required]
        [StringLength(50, MinimumLength = 1)]
        public string Provider { get; set; } = null!;

        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string ProviderKey { get; set; } = null!;

        public long UserId { get; set; }

        [Required]
        [WithMany(nameof(User.ExternalLogins))]
        public virtual User User { get; set; } = null!;
    }
}
""";
        }

        private static string GetRoleCsData(string appName)
        {
            return $$"""
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Enums;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [SpiderlyEntity]
    public class Role : BusinessObject<int>, IRole
    {
        [DisplayName]
        [Required]
        [StringLength(255, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
        public virtual List<User> Users { get; } = new(); // M2M
        IReadOnlyCollection<IUser> IRole.Users => Users;

        [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
        public virtual List<Permission> Permissions { get; } = new(); // M2M
        IReadOnlyCollection<IPermission> IRole.Permissions => Permissions;
    }
}
""";
        }

        private static string GetPermissionCsData(string appName)
        {
            return $$"""
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.BaseEntities;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [Index(nameof(Code), IsUnique = true)]
    [UIDoNotGenerate]
    [SpiderlyEntity]
    public class Permission : ReadonlyObject<int>, IPermission
    {
        [DisplayName]
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Name { get; set; } = null!;

        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string Code { get; set; } = null!;

        public virtual List<Role> Roles { get; } = new(); // M2M
        IReadOnlyCollection<IRole> IPermission.Roles => Roles;
    }
}

""";
        }

        private static string GetRolePermissionCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;

namespace {{appName}}.Business.Entities
{
    [M2M]
    [SpiderlyEntity]
    public class RolePermission
    {
        [M2MWithMany(nameof(Role.Permissions))]
        public virtual Role Role { get; set; } = null!;

        [M2MWithMany(nameof(Permission.Roles))]
        public virtual Permission Permission { get; set; } = null!;
    }
}

""";
        }

        private static string GetUserRoleCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;

namespace {{appName}}.Business.Entities
{
    [M2M]
    [SpiderlyEntity]
    public class UserRole
    {
        [M2MWithMany(nameof(User.Roles))]
        public virtual User User { get; set; } = null!;

        [M2MWithMany(nameof(Role.Users))]
        public virtual Role Role { get; set; } = null!;
    }
}

""";
        }

        private static string GetOutboxMessageCsData(string appName)
        {
            return $$"""
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    /// <summary>
    /// Transactional-outbox row, written inside the same transaction as the entity change it represents
    /// (via Spiderly's IOutbox). The recurring OutboxDispatcherJob sweeps pending rows (DispatchedAt IS NULL)
    /// and routes each to the IOutboxHandler whose Code matches HandlerCode. Payload carries semantic intent
    /// (e.g. ids), not rendered content.
    ///
    /// Surfaced as a read-only Spiderly admin page; operators act through the OutboxMessageController
    /// Retry / Dismiss endpoints, and OutboxMessageService default-filters the list to pending rows.
    /// </summary>
    [SpiderlyEntity]
    public class OutboxMessage : BusinessObject<long>, IOutboxMessage
    {
        [Required]
        [StringLength(100, MinimumLength = 1)]
        public string HandlerCode { get; set; } = null!;

        [Required]
        [StringLength(4000, MinimumLength = 1)]
        public string Payload { get; set; } = null!;

        public DateTime? DispatchedAt { get; set; }

        [Required]
        public int AttemptCount { get; set; }

        public DateTime? LastAttemptedAt { get; set; }

        [StringLength(2000)]
        public string? LastError { get; set; }

        public DateTime? NextAttemptAt { get; set; }

        public long? DismissedByUserId { get; set; }
    }
}
""";
        }
    }
}
