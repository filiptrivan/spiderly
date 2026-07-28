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

        /// <summary>
        /// Opts into the framework's per-endpoint policy cache. The <c>perm:&lt;code&gt;</c> policies materialized here
        /// are deterministic per policy name (same requirement set every time), and the per-user permission check runs
        /// on every request in the <c>PermissionAuthorizationHandler</c> regardless — so caching the static policy
        /// shape is safe and avoids rebuilding it (and the combined policy) on every authorized request. Without this,
        /// <c>AuthorizationMiddleware</c> bypasses its cache for this provider and calls <see cref="GetPolicyAsync"/>
        /// per request (the default-interface value is <c>false</c> for any non-<c>DefaultAuthorizationPolicyProvider</c>).
        /// </summary>
        public bool AllowsCachingPolicies => true;

        /// <inheritdoc/>
        public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallbackPolicyProvider.GetDefaultPolicyAsync();

        /// <inheritdoc/>
        public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallbackPolicyProvider.GetFallbackPolicyAsync();

        /// <inheritdoc/>
        public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
        {
            if (SpiderlyAuthorizationPolicies.TryGetPermissionCode(policyName, out string? permissionCode))
            {
                AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permissionCode))
                    .Build();

                return Task.FromResult<AuthorizationPolicy?>(policy);
            }

            return _fallbackPolicyProvider.GetPolicyAsync(policyName);
        }
    }
}
