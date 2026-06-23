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

        /// <summary>
        /// Authorization check restricted to a <b>human user</b>: it resolves <paramref name="permissionCode"/>
        /// against the <typeparamref name="TUser"/> table by the current principal's id, so a non-user principal
        /// (e.g. an API key) is denied because it isn't in that table. Use this to mark an operation as
        /// users-only; for an operation any principal kind may perform, use the non-generic
        /// <see cref="IsAuthorizedAsync(string)"/>.
        /// </summary>
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
        /// account, API key, …) by its kind through the <see cref="IPrincipalRegistry"/> rather than a
        /// compile-time user type, so the same endpoint authorizes correctly whatever kind of principal is
        /// calling. This is the default — it's what generated CRUD authorization calls. Use the generic
        /// <see cref="IsAuthorizedAsync{TUser}(string)"/> overload instead only when the operation must be
        /// performed by a <b>human user</b> (it resolves against the user table, so any non-user principal is denied).
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
        /// current principal lacks <paramref name="permissionCode"/>. This is the default; use the generic
        /// <c>AuthorizeAndThrowAsync&lt;TUser&gt;</c> overload only when the operation must be performed by a
        /// <b>human user</b> (a non-user principal is then denied — see <see cref="IsAuthorizedAsync(string)"/>).
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

        /// <summary>
        /// The distinct permission codes held by the <b>current principal of any kind</b> — resolved through the
        /// <see cref="IPrincipalRegistry"/> by the <c>principal_kind</c> claim, mirroring <see cref="IsAuthorizedAsync(string)"/>.
        /// Prefer this over the generic <see cref="GetCurrentUserPermissionCodes{TUser, TRole}"/> when the caller
        /// may be a non-human principal (a service account, an API key, …) — e.g. for a "you can only grant what
        /// you already hold" check that must stay correct whatever kind of principal is acting. Returns an empty
        /// list when the principal kind is unrecognized (fail closed).
        /// </summary>
        /// <returns>The current principal's distinct permission codes, or an empty list if it can't be resolved.</returns>
        public virtual async Task<List<string>> GetCurrentPrincipalPermissionCodesAsync()
        {
            if (_principalRegistry.IsEmpty)
                throw new InvalidOperationException(
                    "No principal kinds are registered, so authorization cannot resolve the current principal. " +
                    "Call AddSpiderlyPrincipal<TPrincipal>(\"kind\") (the spiderly init template registers User).");

            if (_principalRegistry.TryResolve(_authenticationService.GetCurrentPrincipalKind(), out IPrincipalPermissionResolver resolver) == false)
                return new List<string>();

            long principalId = _authenticationService.GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
                await resolver.GetPermissionCodesAsync(_context, principalId));
        }

    }
}
