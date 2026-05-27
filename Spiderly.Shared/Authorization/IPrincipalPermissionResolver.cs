using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Authorization
{
    /// <summary>
    /// Resolves the effective permissions of one principal kind (e.g. <c>"User"</c>, <c>"ServiceAccount"</c>).
    /// One implementation is registered per kind via <c>AddSpiderlyPrincipal&lt;TPrincipal&gt;</c>; the
    /// authorization core picks the right one by <see cref="Kind"/> at request time. The interface surface is
    /// intentionally free of any security-layer type so it can live in the shared layer alongside
    /// <see cref="IApplicationDbContext"/>; the concrete principal entity type is bound by the implementation.
    /// </summary>
    public interface IPrincipalPermissionResolver
    {
        /// <summary>Stable discriminator carried in the <c>principal_kind</c> claim.</summary>
        string Kind { get; }

        /// <summary>Whether the principal with the given id holds <paramref name="permissionCode"/>.</summary>
        Task<bool> HasPermissionAsync(IApplicationDbContext context, long principalId, string permissionCode);

        /// <summary>The distinct permission codes granted to the principal with the given id.</summary>
        Task<List<string>> GetPermissionCodesAsync(IApplicationDbContext context, long principalId);
    }
}
