using Microsoft.Extensions.DependencyInjection;
using Spiderly.Security.Authorization;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Authorization;

namespace Spiderly.Security.Extensions
{
    /// <summary>
    /// Registers principal kinds with the authorization core. An application with a single principal kind
    /// (just <c>User</c>) registers one; an application with machine/service principals registers each kind.
    /// <example>
    /// <code>
    /// services.AddSpiderlyPrincipal&lt;User&gt;("User");
    /// services.AddSpiderlyPrincipal&lt;ServiceAccount&gt;("ServiceAccount");
    /// </code>
    /// </example>
    /// </summary>
    public static class PrincipalRegistrationExtensions
    {
        /// <summary>
        /// Registers <typeparamref name="TPrincipal"/> as a principal kind under <paramref name="kind"/>,
        /// using the standard role-based <see cref="RolePermissionResolver{TPrincipal}"/>. The resolver is a
        /// stateless singleton; <see cref="PrincipalRegistry"/> picks it by kind at request time.
        /// </summary>
        public static IServiceCollection AddSpiderlyPrincipal<TPrincipal>(this IServiceCollection services, string kind)
            where TPrincipal : class, ISecurityPrincipal, new()
        {
            services.AddSingleton<IPrincipalPermissionResolver>(new RolePermissionResolver<TPrincipal>(kind));
            return services;
        }
    }
}
