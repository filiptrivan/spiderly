using System;
using Microsoft.AspNetCore.Authorization;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Authorization requirement satisfied when the current principal holds a specific permission code.
    /// Materialized per code by <see cref="PermissionPolicyProvider"/> and evaluated by the consumer's
    /// permission authorization handler, which delegates to the registered authorization service so the
    /// principal-kind dispatch (and any consumer override, e.g. an API-key role cap) is preserved.
    /// </summary>
    public sealed class PermissionRequirement : IAuthorizationRequirement
    {
        /// <summary>The permission code the current principal must hold (e.g. <c>UpdateProduct</c>).</summary>
        public string PermissionCode { get; }

        /// <summary>Creates the requirement for <paramref name="permissionCode"/>.</summary>
        /// <param name="permissionCode">The required permission code; must not be null or empty.</param>
        public PermissionRequirement(string permissionCode)
        {
            if (string.IsNullOrEmpty(permissionCode))
                throw new ArgumentException("Permission code must be provided.", nameof(permissionCode));

            PermissionCode = permissionCode;
        }
    }
}
