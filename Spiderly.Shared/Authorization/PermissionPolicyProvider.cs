using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Dynamic <see cref="IAuthorizationPolicyProvider"/> that materializes a permission policy on demand for any
    /// policy name in the <see cref="SpiderlyAuthorizationPolicies.PermissionPolicyPrefix"/> convention (e.g.
    /// <c>perm:UpdateProduct</c>) — so permission codes need not be pre-registered as named policies. The
    /// materialized policy requires an authenticated user plus a <see cref="PermissionRequirement"/>; every other
    /// policy name falls through to the default provider. Registered as a singleton.
    /// </summary>
    public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
    {
        private readonly DefaultAuthorizationPolicyProvider _fallbackPolicyProvider;

        /// <summary>Creates the provider, wrapping the default provider for non-permission policy names.</summary>
        /// <param name="options">The authorization options used by the wrapped default provider.</param>
        public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        {
            _fallbackPolicyProvider = new DefaultAuthorizationPolicyProvider(options);
        }

        /// <inheritdoc/>
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

        /// <inheritdoc/>
        public Task<AuthorizationPolicy> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

        /// <inheritdoc/>
        public Task<AuthorizationPolicy> GetPolicyAsync(string policyName)
        {
            if (SpiderlyAuthorizationPolicies.TryGetPermissionCode(policyName, out string permissionCode))
            {
                AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permissionCode))
                    .Build();

                return Task.FromResult(policy);
            }

            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}
