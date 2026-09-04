using Spiderly.Security.Interfaces;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// The <see cref="IUser"/> double every fixture in this project needs to close
    /// <c>ApplicationDbContext&lt;TUser&gt;</c>. Shared because it carries no test-specific behavior — two
    /// identical private copies is how a fourth appears.
    /// <para>
    /// PUBLIC and UNSEALED to match what <c>spiderly init</c> emits (<c>public class User</c>), which
    /// stopped being cosmetic once the base <c>OnModelCreating</c> began registering the user entity
    /// for its account-key constraint: an entity in the model must satisfy the proxy rules, and this
    /// double previously escaped them only by never being in one.
    /// </para>
    /// </summary>
    public class TestUser : BusinessObject<long>, IUser
    {
        public string Email { get; set; } = null!;
        public bool? IsDisabled { get; set; }
        public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
    }
}
