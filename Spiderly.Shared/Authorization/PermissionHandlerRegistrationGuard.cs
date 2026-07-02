using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Boot-time guard that fails loud when <c>[HasPermission]</c> is usable but unsatisfiable. When
    /// authentication is enabled, <c>AddSpiderly</c> registers the <see cref="PermissionPolicyProvider"/>, which
    /// materializes a <c>perm:&lt;code&gt;</c> policy carrying a <see cref="PermissionRequirement"/>. The handler
    /// that actually satisfies that requirement (<c>PermissionAuthorizationHandler</c>) lives in
    /// <c>Spiderly.Security</c> and is registered by <c>AddSpiderlyAuthorization&lt;TAuthorizationService&gt;()</c>.
    /// <c>AddSpiderly</c> lives in <c>Spiderly.Shared</c>, which cannot reference <c>Spiderly.Security</c>, so it
    /// cannot register the handler itself — leaving a gap: a consumer who forgets that call gets an authorization
    /// requirement with <b>no handler</b>, which can never <c>Succeed()</c>, so <b>every</b> permission-gated
    /// endpoint silently returns 403 even for a fully-permissioned principal (in-action <c>IsAuthorizedAsync</c>
    /// checks keep working, which makes it especially confusing). This guard converts that silent runtime 403 into
    /// an actionable startup failure.
    /// </summary>
    public sealed class PermissionHandlerRegistrationGuard : IStartupFilter
    {
        private readonly IServiceCollection _services;

        /// <summary>Creates the guard over the application's service collection.</summary>
        /// <param name="services">
        /// The same <see cref="IServiceCollection"/> the host builds its provider from; inspected at startup (after
        /// all registrations, including a consumer's <c>AddSpiderlyAuthorization</c> that may run after <c>AddSpiderly</c>).
        /// </param>
        public PermissionHandlerRegistrationGuard(IServiceCollection services)
        {
            _services = services ?? throw new ArgumentNullException(nameof(services));
        }

        /// <inheritdoc/>
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
        {
            if (HasPermissionRequirementHandler(_services) == false)
                throw new InvalidOperationException(
                    "Spiderly authentication is enabled, so [HasPermission] materializes a PermissionRequirement policy, " +
                    "but no IAuthorizationHandler that satisfies PermissionRequirement is registered. Every permission-gated " +
                    "endpoint would return 403 even for a fully-permissioned principal. Register the handler by calling " +
                    "services.AddSpiderlyAuthorization<TAuthorizationService>() (e.g. AddSpiderlyAuthorization<AuthorizationServiceGenerated>()) " +
                    "after AddSpiderly — the spiderly init template includes this call.");

            return next;
        }

        /// <summary>
        /// True when some descriptor registers an <see cref="IAuthorizationHandler"/> whose implementation derives
        /// from <see cref="AuthorizationHandler{TRequirement}"/> for <see cref="PermissionRequirement"/>. Checked by
        /// type so it does not need to reference the concrete <c>PermissionAuthorizationHandler</c> (which lives in
        /// <c>Spiderly.Security</c>); a consumer's custom handler for the same requirement satisfies it too, whether
        /// it is registered by implementation type or as a pre-built instance.
        /// <para>Known limitation (fail-closed): a <b>factory</b> registration (both <c>ImplementationType</c> and
        /// <c>ImplementationInstance</c> null) cannot be introspected without invoking the factory — which would
        /// instantiate the handler graph at boot — so such a handler is not seen and, if it is the only one, produces
        /// a false boot failure. Accepted trade-off: it fails loud and rare, never back to the silent 403 the guard
        /// exists to prevent, and the built-in <c>AddSecurity</c> bundle registers by type. Resolving the built
        /// provider to close the gap costs more than the edge is worth.</para>
        /// </summary>
        /// <param name="services">The service collection to inspect.</param>
        public static bool HasPermissionRequirementHandler(IServiceCollection services)
        {
            Type permissionHandlerType = typeof(AuthorizationHandler<PermissionRequirement>);

            // Match the implementation type, or the runtime type of a pre-built instance. IsAssignableFrom(null) is
            // false, so a factory registration (both null) falls through undetected — see the remarks above.
            return services.Any(descriptor =>
                descriptor.ServiceType == typeof(IAuthorizationHandler) &&
                permissionHandlerType.IsAssignableFrom(descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()));
        }
    }
}
