using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Shared;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using System.Linq;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides services for accessing authentication-related information from the current HTTP context,
    /// such as the current user's ID, email, access token, and IP address.
    /// </summary>
    public class AuthenticationService : ServiceBase, ICurrentUserAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ISpiderlyPrincipalAccessor _principalAccessor;
        private readonly IApplicationDbContext _context;
        private readonly AuthPolicyOptions _authPolicySettings;
        private readonly TokenKeyOptions _tokenKeySettings;
        private readonly CookieManager _cookieManager;
        private readonly PrincipalIdentity _principalIdentity;

        public AuthenticationService(
            IHttpContextAccessor httpContextAccessor,
            ISpiderlyPrincipalAccessor principalAccessor,
            IApplicationDbContext context,
            IStringLocalizer localizer,
            IOptions<AuthPolicyOptions> authPolicyOptions,
            IOptions<TokenKeyOptions> tokenKeyOptions,
            CookieManager cookieManager,
            PrincipalIdentity principalIdentity
        )
            : base(context, localizer)
        {
            _httpContextAccessor = httpContextAccessor;
            _principalAccessor = principalAccessor;
            _context = context;
            _authPolicySettings = authPolicyOptions.Value;
            _tokenKeySettings = tokenKeyOptions.Value;
            _cookieManager = cookieManager;
            _principalIdentity = principalIdentity;
        }

        /// <summary>
        /// The current <b>human</b> user's id.
        /// </summary>
        /// <remarks>
        /// Throws <see cref="PrincipalKindMismatchException"/> when the caller is a machine principal (an API key,
        /// a service account): its id belongs to its own table and would silently resolve onto an unrelated user
        /// account. For work that is about the caller rather than about a person's data — authorization, auditing,
        /// rate limiting — use <see cref="GetCurrentPrincipalId"/>. Throws
        /// <see cref="System.InvalidOperationException"/> when there is no authenticated principal at all; call
        /// <see cref="GetCurrentUserIdOrDefault"/> on paths that may run anonymously or outside an HTTP request.
        /// </remarks>
        public virtual long GetCurrentUserId()
        {
            return _principalIdentity.GetUserId(_principalAccessor.Current)
                ?? throw new System.InvalidOperationException("There is no authenticated principal in the current execution context.");
        }

        /// <summary>
        /// The current human user's id, or <c>null</c> when there is none — an anonymous context, <b>or</b> a
        /// machine principal. A machine caller genuinely has no user id, which is the same answer as "nobody is
        /// logged in", so this soft read degrades rather than throwing.
        /// </summary>
        public virtual long? GetCurrentUserIdOrDefault()
        {
            SpiderlyPrincipal current = _principalAccessor.Current;

            return _principalIdentity.IsHuman(current) ? current.PrincipalId : null;
        }

        /// <summary>
        /// The current principal's id whatever its kind — a user id for a person, a key/service-account id for a
        /// machine. The right accessor for authorization, auditing and rate limiting, which care about the caller
        /// rather than about whose data is being read.
        /// </summary>
        public virtual long? GetCurrentPrincipalId()
        {
            return _principalAccessor.Current.PrincipalId;
        }

        /// <summary>
        /// The kind of the current principal, from the <c>principal_kind</c> claim, or <c>null</c> when
        /// the claim is absent (the single-principal case). The authorization core's registry resolves a
        /// <c>null</c> kind to the single registered kind, so single-principal applications carry no claim ceremony.
        /// </summary>
        public virtual string GetCurrentPrincipalKind()
        {
            return _principalAccessor.Current.Kind;
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
            return Helper.GetAccessTokenFromCookie(_httpContextAccessor.HttpContext, _tokenKeySettings.AccessTokenKey);
        }

        public virtual string GetIPAddress()
        {
            return Helper.GetIPAddress(_httpContextAccessor.HttpContext);
        }

        public virtual string GetRefreshTokenFromCookie()
        {
            return _httpContextAccessor.HttpContext?.Request.Cookies[_tokenKeySettings.RefreshTokenKey];
        }

        public virtual void SetRefreshTokenCookie(string refreshToken)
        {
            SetCookie(_tokenKeySettings.RefreshTokenKey, refreshToken, _authPolicySettings.RefreshTokenExpiration, httpOnly: true);
        }

        public virtual void ClearRefreshTokenCookie()
        {
            ClearCookie(_tokenKeySettings.RefreshTokenKey, httpOnly: true);
        }

        public virtual void SetAccessTokenCookie(string accessToken)
        {
            SetCookie(_tokenKeySettings.AccessTokenKey, accessToken, _authPolicySettings.AccessTokenExpiration, httpOnly: true);
        }

        public virtual void ClearAccessTokenCookie()
        {
            ClearCookie(_tokenKeySettings.AccessTokenKey, httpOnly: true);
        }

        public virtual void SetAuthResultCookie(AuthResultWithCookiesDTO authResult)
        {
            string json = JsonSerializer.Serialize(authResult);
            SetCookie(_tokenKeySettings.AuthResultKey, json, _authPolicySettings.RefreshTokenExpiration, httpOnly: false);
        }

        public virtual void ClearAuthResultCookie()
        {
            ClearCookie(_tokenKeySettings.AuthResultKey, httpOnly: false);
        }

        private void SetCookie(string key, string value, int expirationMinutes, bool httpOnly)
        {
            CookieOptions cookieOptions = _cookieManager.GetCookieOptions(expirationMinutes, httpOnly);
            _httpContextAccessor.HttpContext.Response.Cookies.Append(key, value, cookieOptions);
        }

        private void ClearCookie(string key, bool httpOnly)
        {
            _cookieManager.ClearCookie(_httpContextAccessor.HttpContext.Response.Cookies, key, httpOnly);
        }
    }
}
