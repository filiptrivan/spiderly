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
    /// services.AddSpiderlyPrincipal&lt;User&gt;("User", PrincipalNature.Human);
    /// services.AddSpiderlyPrincipal&lt;ServiceAccount&gt;("ServiceAccount", PrincipalNature.Machine);
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
        /// <param name="services">The service collection.</param>
        /// <param name="kind">The stable discriminator carried in the <c>principal_kind</c> claim.</param>
        /// <param name="nature">
        /// Whether this kind is a person or a machine. Required rather than defaulted: the framework cannot infer
        /// it from a custom kind's name, and a silent default is exactly what let a type parameter be re-read as a
        /// policy decision it was never chosen to be.
        /// </param>
        public static IServiceCollection AddSpiderlyPrincipal<TPrincipal>(
            this IServiceCollection services, string kind, PrincipalNature nature)
            where TPrincipal : class, ISecurityPrincipal, new()
        {
            // The parameter stays REQUIRED (a silent default is what let a type argument be re-read as a policy
            // decision it was never chosen to be), but it may not contradict the type: IUser is the framework's own
            // structural statement that a principal is a person, and a kind declared Machine while implementing it
            // would invert the fail-closed guarantee this whole split exists for.
            if (nature == PrincipalNature.Machine && typeof(IUser).IsAssignableFrom(typeof(TPrincipal)))
                throw new InvalidOperationException(
                    $"Principal kind '{kind}' is registered as {nameof(PrincipalNature.Machine)}, but " +
                    $"{typeof(TPrincipal).Name} implements {nameof(IUser)} — it is a person. Register it as " +
                    $"{nameof(PrincipalNature)}.{nameof(PrincipalNature.Human)}, or stop implementing {nameof(IUser)}.");

            services.AddSingleton<IPrincipalPermissionResolver>(new RolePermissionResolver<TPrincipal>(kind, nature));
            return services;
        }
    }
}
