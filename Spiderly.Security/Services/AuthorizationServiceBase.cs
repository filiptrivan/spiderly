using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Exceptions;
using Spiderly.Security.Interfaces;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides authorization services, allowing to check if a principal has specific permissions
    /// based on their roles and the permissions associated with those roles.
    /// </summary>
    public class AuthorizationServiceBase : ServiceBase
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly IPrincipalRegistry _principalRegistry;

        public AuthorizationServiceBase(IApplicationDbContext context, AuthenticationService authenticationService, IStringLocalizer localizer, IPrincipalRegistry principalRegistry)
            : base(context, localizer)
        {
            _context = context;
            _authenticationService = authenticationService;
            _principalRegistry = principalRegistry;
        }

        public virtual async Task AuthorizeAndThrowAsync<TUser>(TUser user, string permissionCode) where TUser : class, IUser, new()
        {
            ArgumentNullException.ThrowIfNull(user);
            ArgumentNullException.ThrowIfNull(permissionCode);

            bool result = false;

            await _context.WithTransactionAsync(async () =>
            {
                result = user.Roles.Any(role => role.Permissions.Any(permission => permission.Code == permissionCode));
            });

            if (result == false)
                throw new UnauthorizedException(_localizer["UnauthorizedAccessExceptionMessage"]);
        }

        public virtual async Task<bool> IsAuthorizedAsync<TUser>(string permissionCode) where TUser : class, IUser, new()
        {
            ArgumentNullException.ThrowIfNull(permissionCode);

            bool result = false;
            long userId = _authenticationService.GetCurrentUserId();

            await _context.WithTransactionAsync(async () =>
            {
                result = await _context.DbSet<TUser>()
                    .AsNoTracking()
                    .AnyAsync(user =>
                        user.Id == userId &&
                        user.Roles.Any(role => role.Permissions.Any(permission => permission.Code == permissionCode))
                    );
            });

            return result;
        }

        public virtual async Task AuthorizeAndThrowAsync<TUser>(string permissionCode) where TUser : class, IUser, new()
        {
            ArgumentNullException.ThrowIfNull(permissionCode);

            bool result = false;
            long userId = _authenticationService.GetCurrentUserId();

            await _context.WithTransactionAsync(async () =>
            {
                result = await _context.DbSet<TUser>()
                    .AsNoTracking()
                    .AnyAsync(user =>
                        user.Id == userId &&
                        user.Roles.Any(role => role.Permissions.Any(permission => permission.Code == permissionCode))
                    );
            });

            if (result == false)
                throw new UnauthorizedException(_localizer["UnauthorizedAccessExceptionMessage"]);
        }

        /// <summary>
        /// Principal-kind-agnostic authorization check. Resolves the current principal (human user, service
        /// account, …) by its kind through the <see cref="IPrincipalRegistry"/> rather than a compile-time
        /// user type, so the same endpoint authorizes correctly whatever kind of principal is calling. This
        /// is what generated CRUD authorization calls; prefer it over the generic
        /// <c>IsAuthorizedAsync&lt;TUser&gt;</c> overload.
        /// </summary>
        public virtual async Task<bool> IsAuthorizedAsync(string permissionCode)
        {
            ArgumentNullException.ThrowIfNull(permissionCode);

            // No principal kinds registered is a developer misconfiguration — fail loud at the framework level.
            if (_principalRegistry.IsEmpty)
                throw new InvalidOperationException(
                    "No principal kinds are registered, so authorization cannot resolve the current principal. " +
                    "Call AddSpiderlyPrincipal<TPrincipal>(\"kind\") (the spiderly init template registers User).");

            // An unrecognized principal for THIS request (unknown principal_kind, or a missing claim while
            // multiple kinds are registered) is an authentication-level failure, not a server error: fail
            // closed (deny) so AuthorizeAndThrowAsync surfaces 401/403 rather than a 500.
            if (_principalRegistry.TryResolve(_authenticationService.GetCurrentPrincipalKind(), out IPrincipalPermissionResolver resolver) == false)
                return false;

            long principalId = _authenticationService.GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
                await resolver.HasPermissionAsync(_context, principalId, permissionCode));
        }

        /// <summary>
        /// Principal-kind-agnostic authorization check that throws <see cref="UnauthorizedException"/> when the
        /// current principal lacks <paramref name="permissionCode"/>. Prefer this over the generic
        /// <c>AuthorizeAndThrowAsync&lt;TUser&gt;</c> overload.
        /// </summary>
        public virtual async Task AuthorizeAndThrowAsync(string permissionCode)
        {
            if (await IsAuthorizedAsync(permissionCode) == false)
                throw new UnauthorizedException(_localizer["UnauthorizedAccessExceptionMessage"]);
        }

        public virtual async Task<List<string>> GetCurrentUserPermissionCodes<TUser, TRole>()
            where TUser : class, IUser, new()
            where TRole : class, IRole, new()
        {
            long userId = _authenticationService.GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>()
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .SelectMany(x => x.Roles)
                    .SelectMany(x => x.Permissions)
                    .Select(x => x.Code)
                    .Distinct()
                    .ToListAsync();
            });
        }

    }
}
