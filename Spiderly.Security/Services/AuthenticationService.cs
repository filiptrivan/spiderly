using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides services for accessing authentication-related information from the current HTTP context,
    /// such as the current user's ID, email, access token, and IP address.
    /// </summary>
    public class AuthenticationService : ServiceBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationDbContext _context;

        public AuthenticationService(
            IHttpContextAccessor httpContextAccessor,
            IApplicationDbContext context,
            IStringLocalizer localizer
        )
            : base(context, localizer)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }

        public virtual long GetCurrentUserId()
        {
            return Helper.GetCurrentUserId(_httpContextAccessor.HttpContext);
        }

        public virtual long? GetCurrentUserIdOrDefault()
        {
            return Helper.GetCurrentUserIdOrDefault(_httpContextAccessor.HttpContext);
        }

        public virtual async Task<string> GetCurrentUserEmail<TUser>() where TUser : class, IUser, new()
        {
            long currentUserId = GetCurrentUserId();

            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Id == currentUserId).Select(x => x.Email).SingleAsync();
            });
        }

        public virtual async Task<TUser> GetCurrentUser<TUser>() where TUser : class, IUser, new()
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await GetInstanceAsync<TUser, long>(GetCurrentUserId(), null);
            });
        }

        public virtual async Task<UserBaseDTO> GetCurrentUserBaseDTO<TUser>() where TUser : class, IUser, new()
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

        public virtual string GetAccessTokenFromHeader()
        {
            return Helper.GetAccessTokenFromHeader(_httpContextAccessor.HttpContext);
        }

        public virtual string GetAccessTokenFromCookie()
        {
            return Helper.GetAccessTokenFromCookie(_httpContextAccessor.HttpContext);
        }

        public virtual string GetIPAddress()
        {
            return Helper.GetIPAddress(_httpContextAccessor.HttpContext);
        }

        public virtual string GetRefreshTokenFromCookie()
        {
            return _httpContextAccessor.HttpContext?.Request.Cookies[SettingsProvider.Current.RefreshTokenKey];
        }

        public virtual void SetRefreshTokenCookie(string refreshToken)
        {
            SetCookie(SettingsProvider.Current.RefreshTokenKey, refreshToken, SettingsProvider.Current.RefreshTokenExpiration, httpOnly: true);
        }

        public virtual void ClearRefreshTokenCookie()
        {
            ClearCookie(SettingsProvider.Current.RefreshTokenKey, httpOnly: true);
        }

        public virtual void SetAccessTokenCookie(string accessToken)
        {
            SetCookie(SettingsProvider.Current.AccessTokenKey, accessToken, SettingsProvider.Current.AccessTokenExpiration, httpOnly: true);
        }

        public virtual void ClearAccessTokenCookie()
        {
            ClearCookie(SettingsProvider.Current.AccessTokenKey, httpOnly: true);
        }

        public virtual void SetAuthResultCookie(AuthResultWithCookiesDTO authResult)
        {
            string json = JsonSerializer.Serialize(authResult);
            SetCookie(SettingsProvider.Current.AuthResultKey, json, SettingsProvider.Current.RefreshTokenExpiration, httpOnly: false);
        }

        public virtual void ClearAuthResultCookie()
        {
            ClearCookie(SettingsProvider.Current.AuthResultKey, httpOnly: false);
        }

        private void SetCookie(string key, string value, int expirationMinutes, bool httpOnly)
        {
            CookieOptions cookieOptions = CookieHelper.GetCookieOptions(expirationMinutes, httpOnly);
            _httpContextAccessor.HttpContext.Response.Cookies.Append(key, value, cookieOptions);
        }

        private void ClearCookie(string key, bool httpOnly)
        {
            CookieHelper.ClearCookie(_httpContextAccessor.HttpContext.Response.Cookies, key, httpOnly);
        }
    }
}
