using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Exceptions;
using Spiderly.Security.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides authorization services, allowing to check if a user has specific permissions
    /// based on their roles and the permissions associated with those roles.
    /// </summary>
    public class AuthorizationService : BusinessServiceBase
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;

        public AuthorizationService(IApplicationDbContext context, AuthenticationService authenticationService)
            : base(context)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

        public async Task AuthorizeAndThrowAsync<TUser>(TUser user, string permissionCode) where TUser : class, IUser, new()
        {
            if (user == null)
                throw new ArgumentNullException("The user is not provided.");

            if (permissionCode == null)
                throw new ArgumentNullException("Permission code is not provided.");

            bool result = false;

            await _context.WithTransactionAsync(async () =>
            {
                result = user.Roles.Any(role => role.Permissions.Any(permission => permission.Code == permissionCode));
            });

            if (result == false)
                throw new UnauthorizedException();
        }

        public async Task<bool> IsAuthorizedAsync<TUser>(string permissionCode) where TUser : class, IUser, new()
        {
            if (permissionCode == null)
                throw new ArgumentNullException("Permission code is not provided.");

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

        public async Task AuthorizeAndThrowAsync<TUser>(string permissionCode) where TUser : class, IUser, new()
        {
            if (permissionCode == null)
                throw new ArgumentNullException("Permission code is not provided.");

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
                throw new UnauthorizedException();
        }

        public async Task<List<string>> GetCurrentUserPermissionCodes<TUser>() where TUser : class, IUser, new()
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
