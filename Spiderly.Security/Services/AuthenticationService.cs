using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides services for accessing authentication-related information from the current HTTP context,
    /// such as the current user's ID, email, access token, and IP address.
    /// </summary>
    public class AuthenticationService : BusinessServiceBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationDbContext _context;

        public AuthenticationService(
            IHttpContextAccessor httpContextAccessor,
            IApplicationDbContext context
        )
            : base(context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public long GetCurrentUserId()
        {
            return Helper.GetCurrentUserId(_httpContextAccessor.HttpContext);
        }

        public async Task<string> GetCurrentUserEmail<TUser>() where TUser : class, IUser, new()
        {
            long currentUserId = GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Id == currentUserId).Select(x => x.Email).SingleAsync();
            });
        }

        public async Task<TUser> GetCurrentUser<TUser>() where TUser : class, IUser, new()
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await GetInstanceAsync<TUser, long>(GetCurrentUserId(), null);
            });
        }

        public async Task<UserBaseDTO> GetCurrentUserBaseDTO<TUser>() where TUser : class, IUser, new()
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>()
                    .Where(x => x.Id == GetCurrentUserId())
                    .Select(x => new UserBaseDTO
                    {
                        Id = x.Id,
                        Email = x.Email
                    })
                    .SingleOrDefaultAsync();
            });
        }

        public async Task<string> GetAccessTokenAsync()
        {
            return await _httpContextAccessor.HttpContext.GetTokenAsync("Bearer", "access_token");
        }

        public string GetIPAddress()
        {
            return Helper.GetIPAddress(_httpContextAccessor.HttpContext);
        }

        public string GetRefreshTokenFromCookie()
        {
            return _httpContextAccessor.HttpContext?.Request.Cookies[SettingsProvider.Current.RefreshTokenCookieName];
        }

        public void SetRefreshTokenCookie(string refreshToken)
        {
            CookieOptions cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddMinutes(SettingsProvider.Current.RefreshTokenExpiration)
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append(SettingsProvider.Current.RefreshTokenCookieName, refreshToken, cookieOptions);
        }

        public void ClearRefreshTokenCookie()
        {
            CookieOptions cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(-1)
            };

            _httpContextAccessor.HttpContext.Response.Cookies.Append(SettingsProvider.Current.RefreshTokenCookieName, string.Empty, cookieOptions);
        }
    }
}
