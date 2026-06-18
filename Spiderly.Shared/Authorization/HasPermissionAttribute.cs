using Microsoft.AspNetCore.Authorization;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Requires the current principal to hold a specific permission, enforced at the endpoint boundary through
    /// the permission policy materialized by <see cref="PermissionPolicyProvider"/>. Typed sugar over
    /// <c>[Authorize(Policy = "perm:&lt;code&gt;")]</c> — annotate an action with
    /// <c>[HasPermission("UpdateProduct")]</c> (or a generated CRUD code) instead of hand-typing the policy
    /// string. Composes with <c>[AuthGuard]</c> (authentication); this adds the authorization check.
    /// </summary>
    /// <remarks>
    /// The permission code is a constructor argument (so a compile-time constant string literal or generated
    /// code works as an attribute argument); the <c>perm:</c> policy name is built at attribute construction via
    /// <see cref="SpiderlyAuthorizationPolicies.ForPermission"/>, keeping the convention in one place.
    /// </remarks>
    public sealed class HasPermissionAttribute : AuthorizeAttribute
    {
        /// <summary>Creates the attribute requiring <paramref name="permissionCode"/>.</summary>
        /// <param name="permissionCode">The permission code the caller must hold (e.g. <c>UpdateProduct</c>).</param>
        public HasPermissionAttribute(string permissionCode)
            : base(SpiderlyAuthorizationPolicies.ForPermission(permissionCode))
        {
            PermissionCode = permissionCode;
        }

        /// <summary>The required permission code (also encoded into the policy name).</summary>
        public string PermissionCode { get; }
    }
}
