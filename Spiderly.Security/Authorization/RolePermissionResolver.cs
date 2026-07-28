using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Authorization
{
    /// <summary>
    /// The standard role-based <see cref="IPrincipalPermissionResolver"/>: a principal's permissions are the
    /// union of the permissions of its roles. Written once, generic over the concrete principal entity type,
    /// so adding a new principal kind is a single <c>AddSpiderlyPrincipal&lt;TPrincipal&gt;</c> call with no
    /// new query code. The role → permission walk mirrors the generic authorization methods.
    /// </summary>
    public sealed class RolePermissionResolver<TPrincipal> : IPrincipalPermissionResolver
        where TPrincipal : class, ISecurityPrincipal, new()
    {
        public RolePermissionResolver(string kind, PrincipalNature nature)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            Nature = nature;
        }

        public string Kind { get; }

        public PrincipalNature Nature { get; }

        public async Task<bool> HasPermissionAsync(IApplicationDbContext context, long principalId, string permissionCode)
        {
            return await context.DbSet<TPrincipal>()
                .AsNoTracking()
                .AnyAsync(principal =>
                    principal.Id == principalId &&
                    principal.Roles.Any(role => role.Permissions.Any(permission => permission.Code == permissionCode)));
        }

        public async Task<List<string>> GetPermissionCodesAsync(IApplicationDbContext context, long principalId)
        {
            return await context.DbSet<TPrincipal>()
                .AsNoTracking()
                .Where(principal => principal.Id == principalId)
                .SelectMany(principal => principal.Roles)
                .SelectMany(role => role.Permissions)
                .Select(permission => permission.Code)
                .Distinct()
                .ToListAsync();
        }
    }
}
