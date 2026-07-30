using Spiderly.Security.Interfaces;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// The <see cref="IUser"/> double every fixture in this project needs to close
    /// <c>ApplicationDbContext&lt;TUser&gt;</c>. Shared because it carries no test-specific behavior — two
    /// identical private copies is how a fourth appears.
    /// </summary>
    internal sealed class TestUser : BusinessObject<long>, IUser
    {
        public string Email { get; set; } = null!;
        public bool? IsDisabled { get; set; }
        public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
    }
}
