using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.SecurityControllers // Needs to be other namespace because of source generator
{
    /// <summary>
    /// A base controller providing core security functionalities such as authentication, user management, and role-based access control.
    /// It leverages various services for handling user authentication (login, registration, logout, token refresh).    /// This controller is designed to be extended for specific user types.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    /// <typeparam name="TRole">The type of the role entity, which must implement the <see cref="IRole"/> interface.</typeparam>
    /// <typeparam name="TUserExternalLogin">The entity linking a user to an external provider login, implementing <see cref="IUserExternalLogin"/>.</typeparam>
    public class SecurityBaseController<TUser, TRole, TUserExternalLogin> : SpiderlyBaseController
        where TUser : class, IUser, new()
        where TRole : class, IRole, new()
        where TUserExternalLogin : class, IUserExternalLogin, new()
    {
        private readonly SecurityServiceBase<TUser, TUserExternalLogin> _securityServiceBase;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly AuthorizationServiceBase _authorizationServiceBase;

        public SecurityBaseController(
            SecurityServiceBase<TUser, TUserExternalLogin> securityServiceBase,
            IJwtAuthManager jwtAuthManagerService,
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            AuthorizationServiceBase authorizationServiceBase
        )
        {
            _securityServiceBase = securityServiceBase;
            _jwtAuthManagerService = jwtAuthManagerService;
            _context = context;
            _authenticationService = authenticationService;
            _authorizationServiceBase = authorizationServiceBase;
        }

        #region Authentication

        [HttpPost]
        public virtual async Task<SendLoginVerificationEmailResultDTO> SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            return await _securityServiceBase.SendLoginVerificationEmail(loginDTO);
        }

        [HttpPost]
        public virtual async Task<AuthResultDTO> Login(VerificationTokenRequestDTO request)
        {
            return await _securityServiceBase.Login(request);
        }

        [HttpPost]
        [UIDoNotGenerate]
        public virtual async Task<AuthResultDTO> LoginExternal(ExternalProviderDTO externalProviderDTO)
        {
            string protectedNonce = Request.Cookies[ExternalLoginNonceCookieName];
            try
            {
                return await _securityServiceBase.LoginExternal(externalProviderDTO, protectedNonce);
            }
            finally
            {
                ClearExternalLoginNonceCookie(); // single-use
            }
        }

        [HttpPost]
        public virtual async Task<AuthResultWithCookiesDTO> LoginWithCookies(VerificationTokenRequestDTO request)
        {
            return await _securityServiceBase.LoginWithCookies(request);
        }

        [HttpPost]
        [UIDoNotGenerate]
        public virtual async Task<AuthResultWithCookiesDTO> LoginExternalWithCookies(ExternalProviderDTO externalProviderDTO)
        {
            string protectedNonce = Request.Cookies[ExternalLoginNonceCookieName];
            try
            {
                return await _securityServiceBase.LoginExternalWithCookies(externalProviderDTO, protectedNonce);
            }
            finally
            {
                ClearExternalLoginNonceCookie(); // single-use
            }
        }

        /// <summary>
        /// Public list of enabled external providers (code + OIDC authority + client id + button display),
        /// so the frontend can render sign-in buttons and run the client OIDC flow. Anonymous — the values are
        /// public by OIDC design.
        /// </summary>
        [HttpGet]
        [UIDoNotGenerate]
        public virtual List<ExternalProviderPublicDTO> GetExternalProviders()
        {
            return _securityServiceBase.GetExternalProviders();
        }

        private const string ExternalLoginNonceCookieName = "spiderly_external_login_nonce";

        /// <summary>
        /// Issues a one-time nonce for the client-side (GIS / id-token) external-login flow: returns the raw
        /// nonce for the SPA to pass to the provider's sign-in call (so it is echoed into the id token), and
        /// stores a signed copy in a short-lived HttpOnly cookie that <see cref="LoginExternal"/> /
        /// <see cref="LoginExternalWithCookies"/> verify the returned id token against. Anonymous.
        /// <c>SameSite=None</c> so the cookie rides the cross-site login POST.
        /// </summary>
        [HttpGet]
        [UIDoNotGenerate]
        public virtual ExternalLoginNonceDTO GetExternalLoginNonce()
        {
            (string nonce, string protectedNonce) = _securityServiceBase.CreateExternalLoginNonce();

            Response.Cookies.Append(ExternalLoginNonceCookieName, protectedNonce, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None, // sent on the cross-site fetch POST to LoginExternal(WithCookies)
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(15),
            });

            return new ExternalLoginNonceDTO { Nonce = nonce };
        }

        private void ClearExternalLoginNonceCookie()
        {
            Response.Cookies.Delete(ExternalLoginNonceCookieName, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
            });
        }

        private const string ExternalLoginStateCookieName = "spiderly_external_login_state";

        /// <summary>
        /// Server-side external-login step 1: redirects the browser to the provider's authorize endpoint.
        /// The state/nonce/PKCE-verifier are stored in a short-lived, Data-Protection-signed HttpOnly cookie.
        /// </summary>
        [HttpGet]
        [UIDoNotGenerate]
        public virtual async Task<ActionResult> ExternalLoginChallenge(string provider, string returnUrl, string browserId)
        {
            // The provider redirects back here; this absolute URL must be registered as the provider's redirect URI.
            string redirectUri = Url.Action(nameof(ExternalLoginCallback), null, null, Request.Scheme);

            (string authorizeUrl, string protectedState) = await _securityServiceBase.BeginExternalLoginAsync(provider, returnUrl, browserId, redirectUri);

            Response.Cookies.Append(ExternalLoginStateCookieName, protectedState, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax, // sent on the top-level GET navigation back from the provider
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(10),
            });

            return Redirect(authorizeUrl);
        }

        /// <summary>
        /// Server-side external-login step 2: the provider redirects here with the code. Exchanges it for the
        /// id token (server-side), validates + links the user, issues the session as HttpOnly cookies, and
        /// redirects back to the originating app (returnUrl).
        /// </summary>
        [HttpGet]
        [UIDoNotGenerate]
        public virtual async Task<ActionResult> ExternalLoginCallback(string code, string state)
        {
            string protectedState = Request.Cookies[ExternalLoginStateCookieName];

            string returnUrl = await _securityServiceBase.CompleteExternalLoginAsync(code, state, protectedState);

            Response.Cookies.Delete(ExternalLoginStateCookieName);

            // returnUrl was validated against the configured frontend origin at challenge time (SanitizeReturnUrl).
            return Redirect(returnUrl);
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<ActionResult> Logout(string browserId)
        {
            long userId = _authenticationService.GetCurrentUserId();
            await _jwtAuthManagerService.LogoutAsync(browserId, userId); // If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user

            return Ok();
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<ActionResult> LogoutWithCookies(string browserId)
        {
            long userId = _authenticationService.GetCurrentUserId();
            await _jwtAuthManagerService.LogoutAsync(browserId, userId);

            _authenticationService.ClearRefreshTokenCookie();
            _authenticationService.ClearAccessTokenCookie();
            _authenticationService.ClearAuthResultCookie();

            return Ok();
        }

        /// <summary>
        /// Here we would put [Authorize] attribute, because we don't validate life time of the access token, but we are not because deeper in the method we are validating it without life time also.
        /// </summary>
        [HttpPost]
        public virtual async Task<AuthResultDTO> RefreshTokenWithHeaders(RefreshTokenRequestDTO request)
        {
            return await _securityServiceBase.RefreshTokenWithHeaders(request);
        }

        /// <summary>
        /// Refreshes the access token using the refresh token stored in an HttpOnly cookie.
        /// </summary>
        [HttpGet]
        public virtual async Task<AuthResultWithCookiesDTO> RefreshTokenWithCookies(string browserId)
        {
            return await _securityServiceBase.RefreshTokenWithCookies(browserId);
        }

        #endregion

        #region User

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public virtual async Task<UserBaseDTO> GetCurrentUserBase()
        {
            return await _authenticationService.GetCurrentUserBaseDTO<TUser>();
        }

        [HttpGet]
        [AuthGuard]
        [UIDoNotGenerate]
        public virtual async Task<List<string>> GetCurrentUserPermissionCodes()
        {
            return await _authorizationServiceBase.GetCurrentUserPermissionCodes<TUser, TRole>();
        }

        #endregion

    }
}
