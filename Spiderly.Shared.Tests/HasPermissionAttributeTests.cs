using Spiderly.Shared.Authorization;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // [HasPermission(code)] is the typed entry point the controller generator emits and hand-written endpoints
    // use. It must map the code to the same `perm:<code>` policy name the PermissionPolicyProvider parses.
    public class HasPermissionAttributeTests
    {
        [Fact]
        public void Maps_permission_code_to_the_perm_policy_name()
        {
            HasPermissionAttribute attribute = new("UpdateProduct");

            Assert.Equal("UpdateProduct", attribute.PermissionCode);
            Assert.Equal("perm:UpdateProduct", attribute.Policy);
            Assert.Equal(SpiderlyAuthorizationPolicies.ForPermission("UpdateProduct"), attribute.Policy);
        }
    }
}
