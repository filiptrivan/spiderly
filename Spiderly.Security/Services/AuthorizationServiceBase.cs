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

        // The <TUser> overloads of IsAuthorizedAsync / AuthorizeAndThrowAsync were REMOVED. They resolved the
        // permission against the TUser table by the current principal's id, which denied a machine principal as a
        // side effect of the lookup — so "humans only" was expressed by a type argument rather than stated. In
        // practice it was never chosen: the generic form existed alone from 2026-01, every consumer call site was
        // written against it, and the human-only reading was retro-fitted in a 2026-06 doc edit. Authorization is
        // now principal-agnostic by default (a role IS the statement of what a caller may do); an operation that
        // genuinely requires a person states it separately via AuthenticationService.GetCurrentUserId(), which
        // fails closed on a machine principal.

        /// <summary>
        /// Principal-kind-agnostic authorization check. Resolves the current principal (human user, service
        /// account, API key, …) by its kind through the <see cref="IPrincipalRegistry"/> rather than a
        /// compile-time user type, so the same endpoint authorizes correctly whatever kind of principal is
        /// calling. This is the default, and the only shape: authorization is
        /// principal-agnostic, because a role is already the statement of what a caller may do. An operation that
        /// genuinely requires a person asks for one separately, via
        /// <c>AuthenticationService.GetCurrentUserId()</c>.
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

            // GetCurrentPrincipalId, NOT GetCurrentUserId: this path authorizes ANY principal kind, and the
            // user-id accessor refuses a machine principal by design — reading it here would make every API-key
            // permission check throw instead of resolving.
            long? principalId = _authenticationService.GetCurrentPrincipalId();
            if (principalId.HasValue == false)
                return false;

            return await _context.WithTransactionAsync(async () =>
                await resolver.HasPermissionAsync(_context, principalId.Value, permissionCode));
        }

        /// <summary>
        /// Principal-kind-agnostic authorization check that throws <see cref="UnauthorizedException"/> when the
        /// current principal lacks <paramref name="permissionCode"/>. See <see cref="IsAuthorizedAsync(string)"/>
        /// for why authorization is principal-agnostic.
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

            // Kind-agnostic for the same reason as IsAuthorizedAsync above.
            long? principalId = _authenticationService.GetCurrentPrincipalId();
            if (principalId.HasValue == false)
                return new List<string>();

            return await _context.WithTransactionAsync(async () =>
                await resolver.GetPermissionCodesAsync(_context, principalId.Value));
        }

    }
}
