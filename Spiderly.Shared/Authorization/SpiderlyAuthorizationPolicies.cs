using System;
using System.Diagnostics.CodeAnalysis;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Single source of truth for the permission-policy naming convention that bridges a permission code to an
    /// ASP.NET Core authorization policy name. Annotate an endpoint with
    /// <c>[Authorize(SpiderlyAuthorizationPolicies.ForPermission(PermissionCodes.UpdateProduct))]</c> so the
    /// policy string is never hand-typed; <see cref="PermissionPolicyProvider"/> reverses it on the way in.
    /// </summary>
    public static class SpiderlyAuthorizationPolicies
    {
        /// <summary>Prefix marking a dynamically-materialized permission policy (e.g. <c>perm:UpdateProduct</c>).</summary>
        public const string PermissionPolicyPrefix = "perm:";

        /// <summary>Builds the policy name for a permission code.</summary>
        /// <param name="permissionCode">The permission code (e.g. <c>UpdateProduct</c>); must not be null or empty.</param>
        /// <returns>The policy name, e.g. <c>perm:UpdateProduct</c>.</returns>
        public static string ForPermission(string permissionCode)
        {
            if (string.IsNullOrEmpty(permissionCode))
                throw new ArgumentException("Permission code must be provided.", nameof(permissionCode));

            return PermissionPolicyPrefix + permissionCode;
        }

        /// <summary>Extracts the permission code from a policy name produced by <see cref="ForPermission"/>.</summary>
        /// <param name="policyName">The policy name to inspect.</param>
        /// <param name="permissionCode">The extracted permission code when this is a permission policy; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> when <paramref name="policyName"/> is a permission policy.</returns>
        public static bool TryGetPermissionCode(string policyName, [NotNullWhen(true)] out string? permissionCode)
        {
            if (string.IsNullOrEmpty(policyName) == false
                && policyName.StartsWith(PermissionPolicyPrefix, StringComparison.Ordinal)
                && policyName.Length > PermissionPolicyPrefix.Length)
            {
                permissionCode = policyName.Substring(PermissionPolicyPrefix.Length);
                return true;
            }

            permissionCode = null;
            return false;
        }
    }
}
