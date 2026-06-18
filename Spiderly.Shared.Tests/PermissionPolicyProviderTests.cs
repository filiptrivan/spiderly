using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Authorization;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Permission-as-policy bridge: a permission code <-> ASP.NET policy name convention plus a dynamic policy
    // provider that materializes `perm:<Code>` policies on demand. Pins the convention round-trip and that the
    // materialized policy requires an authenticated user + the right PermissionRequirement, and that non-permission
    // names fall through to the default provider.
    public class PermissionPolicyProviderTests
    {
        [Fact]
        public void ForPermission_builds_prefixed_policy_name()
        {
            Assert.Equal("perm:UpdateProduct", SpiderlyAuthorizationPolicies.ForPermission("UpdateProduct"));
        }

        [Theory]
        [InlineData("perm:UpdateProduct", true, "UpdateProduct")]
        [InlineData("perm:a", true, "a")]
        [InlineData("perm:", false, null)]            // prefix only → not a usable permission policy
        [InlineData("UpdateProduct", false, null)]    // no prefix
        [InlineData("", false, null)]
        [InlineData(null, false, null)]
        public void TryGetPermissionCode_parses_only_permission_policies(string policyName, bool expected, string expectedCode)
        {
            bool result = SpiderlyAuthorizationPolicies.TryGetPermissionCode(policyName, out string code);

            Assert.Equal(expected, result);
            Assert.Equal(expectedCode, code);
        }

        [Fact]
        public async Task GetPolicyAsync_materializes_permission_policy_with_requirement_and_auth()
        {
            PermissionPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));

            AuthorizationPolicy policy = await provider.GetPolicyAsync(SpiderlyAuthorizationPolicies.ForPermission("UpdateProduct"));

            Assert.NotNull(policy);
            PermissionRequirement requirement = policy.Requirements.OfType<PermissionRequirement>().Single();
            Assert.Equal("UpdateProduct", requirement.PermissionCode);
            // RequireAuthenticatedUser() → anonymous callers are denied before the permission check.
            Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
        }

        [Fact]
        public async Task GetPolicyAsync_falls_through_for_non_permission_policy()
        {
            PermissionPolicyProvider provider = new(Options.Create(new AuthorizationOptions()));

            // Not a "perm:" policy and not registered anywhere → the wrapped default provider returns null.
            AuthorizationPolicy policy = await provider.GetPolicyAsync("SomeOtherPolicy");

            Assert.Null(policy);
        }
    }
}
