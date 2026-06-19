using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Security.Authorization;
using Spiderly.Security.Services;

namespace Spiderly.Security.Extensions
{
    /// <summary>
    /// Registers the permission-as-policy authorization that pairs with the framework's
    /// <c>PermissionPolicyProvider</c> (wired in <c>AddSpiderly</c>). Lives here, not in the init template, so the
    /// two coupled registrations stay together and framework-owned — the <see cref="PermissionAuthorizationHandler"/>
    /// can't drift from the provider, and the <see cref="AuthorizationServiceBase"/> forwarding can't be forgotten.
    /// </summary>
    public static class AuthorizationRegistrationExtensions
    {
        /// <summary>
        /// Registers the <c>[HasPermission]</c> handler and forwards the framework's <see cref="AuthorizationServiceBase"/>
        /// to <typeparamref name="TAuthorizationService"/> — the application's generated / most-derived authorization
        /// service — so the handler evaluates that service's <c>IsAuthorizedAsync</c> override (and any API-key cap).
        /// </summary>
        /// <typeparam name="TAuthorizationService">The app's authorization service registered in DI (e.g. <c>AuthorizationServiceGenerated</c>).</typeparam>
        public static IServiceCollection AddSpiderlyAuthorization<TAuthorizationService>(this IServiceCollection services)
            where TAuthorizationService : AuthorizationServiceBase
        {
            // Forward AuthorizationServiceBase to the app's generated authorization service so framework consumers
            // (PermissionAuthorizationHandler) resolve the most-derived IsAuthorizedAsync override.
            services.AddTransient<AuthorizationServiceBase>(sp => sp.GetRequiredService<TAuthorizationService>());

            // Evaluates [HasPermission] / [Authorize("perm:...")] policies materialized by PermissionPolicyProvider.
            services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

            return services;
        }
    }
}
