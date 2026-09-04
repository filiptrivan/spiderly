using Spiderly.Security.Interfaces;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// The <see cref="IUser"/> double every fixture in this project needs to close
    /// <c>ApplicationDbContext&lt;TUser&gt;</c>. Shared because it carries no test-specific behavior — two
    /// identical private copies is how a fourth appears.
    /// <para>
    /// Carries <c>[SpiderlyEntity]</c>, unlike the shape fixtures in this project, because every real
    /// <c>TUser</c> does — it is what puts the user in the model through discovery, with all three
    /// relationship passes applied, which is the only faithful way for a double to get there. PUBLIC
    /// and UNSEALED for the same reason: <c>SpiderlyAddDbContext</c> always enables lazy-loading
    /// proxies, so a real user is a proxied entity and a sealed double could never be one.
    /// </para>
    /// </summary>
    [SpiderlyEntity]
    public class TestUser : BusinessObject<long>, IUser
    {
        public string Email { get; set; } = null!;
        public bool? IsDisabled { get; set; }
        public IReadOnlyCollection<IRole> Roles => Array.Empty<IRole>();
    }
}
