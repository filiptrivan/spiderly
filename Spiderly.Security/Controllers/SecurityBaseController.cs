using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.SecurityControllers // Needs to be other namespace because of source generator
{
    /// <summary>
    /// A base controller providing core security functionalities such as authentication, user management, and role-based access control.
    /// It leverages various services for handling user authentication (login, registration, logout, token refresh).
    /// This controller is designed to be extended for specific user types.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    /// <typeparam name="TRole">The type of the role entity, which must implement the <see cref="IRole"/> interface.</typeparam>
    /// <typeparam name="TUserExternalLogin">The entity linking a user to an external provider login, implementing <see cref="IUserExternalLogin"/>.</typeparam>
    // Auth/session responses carry per-session identity + tokens and MUST never be cached. Without this,
    // browsers cache the GET responses (e.g. RefreshTokenWithCookies) and replay a stale "logged-in" body
    // on back/forward + soft reloads — showing a phantom dashboard after logout until a hard revalidation
    // reveals the 401. NoStore + Location.None emits "Cache-Control: no-store, no-cache" + "Pragma: no-cache".
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
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

        /// <summary>
        /// Passwordless login step 1: sends a short-lived numeric verification code to the user's email. Anonymous.
        /// </summary>
        [HttpPost]
        public virtual async Task<SendLoginVerificationEmailResultDTO> SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            return await _securityServiceBase.SendLoginVerificationEmail(loginDTO);
        }

        /// <summary>
        /// Passwordless login step 2: verifies the emailed code and, on success, returns the access +
        /// refresh tokens in the response body. Anonymous.
        /// </summary>
        [HttpPost]
        public virtual async Task<AuthResultDTO> Login(VerificationTokenRequestDTO request)
        {
            return await _securityServiceBase.Login(request);
        }

        /// <summary>
        /// Client-side external (OIDC) login: validates the provider id token against the single-use nonce
        /// cookie and returns the access + refresh tokens in the response body. Anonymous.
        /// </summary>
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

        /// <summary>
        /// Like <see cref="Login"/>, but issues the session as HttpOnly cookies instead of returning the
        /// tokens in the response body. Anonymous.
        /// </summary>
        [HttpPost]
        public virtual async Task<AuthResultWithCookiesDTO> LoginWithCookies(VerificationTokenRequestDTO request)
        {
            return await _securityServiceBase.LoginWithCookies(request);
        }

        /// <summary>
        /// Like <see cref="LoginExternal"/>, but issues the session as HttpOnly cookies instead of returning
        /// the tokens in the response body. Anonymous.
        /// </summary>
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
                MaxAge = TimeSpan.FromMinutes(15), // matches the nonce lifetime; grace for the provider's account picker
            });

            return Redirect(authorizeUrl);
        }

        /// <summary>
        /// Server-side external-login step 2: the provider redirects here with the code. Exchanges it for the
        /// id token (server-side), validates + links the user, issues the session as HttpOnly cookies, and
        /// redirects back to the originating app (returnUrl).
        /// <para>
        /// This is a top-level browser navigation, so failures must <b>redirect back to the app</b> (never
        /// render a JSON error onto the API origin). The app surfaces a friendly message via the
        /// <c>externalAuthError</c> hint: <c>expired</c> (the state cookie lapsed — the user lingered on the
        /// provider's picker) or <c>failed</c> (invalid state/nonce, failed code exchange, or denied consent).
        /// </para>
        /// </summary>
        [HttpGet]
        [UIDoNotGenerate]
        public virtual async Task<ActionResult> ExternalLoginCallback(string code, string state)
        {
            string protectedState = Request.Cookies[ExternalLoginStateCookieName];
            Response.Cookies.Delete(ExternalLoginStateCookieName); // single-use — clear regardless of outcome

            // No state cookie => it expired (almost always because the user took too long on the provider's
            // account picker). Benign: bounce back with an "expired" hint instead of throwing a security error.
            if (string.IsNullOrEmpty(protectedState))
                return Redirect(_securityServiceBase.BuildExternalAuthErrorRedirectUrl("expired"));

            try
            {
                string returnUrl = await _securityServiceBase.CompleteExternalLoginAsync(code, state, protectedState);
                // returnUrl was validated against the configured frontend origin at challenge time (SanitizeReturnUrl).
                return Redirect(returnUrl);
            }
            catch (Exception ex)
            {
                ILogger logger = HttpContext.RequestServices
                    .GetService<ILogger<SecurityBaseController<TUser, TRole, TUserExternalLogin>>>();

                if (ex is BusinessException || ex is SecurityViolationException)
                {
                    // Expected, caller-driven outcome: denied consent, unverified email, missing code, or a
                    // tampered/expired state/nonce. A single failed attempt is logged for traceability, not
                    // treated as a server fault.
                    logger?.LogWarning(ex, "External login callback rejected.");
                }
                else
                {
                    // Unexpected server fault (e.g. data-store failure while resolving the user). Surface at
                    // Error so it isn't hidden behind the friendly redirect.
                    logger?.LogError(ex, "External login callback failed unexpectedly.");
                }

                return Redirect(_securityServiceBase.BuildExternalAuthErrorRedirectUrl("failed"));
            }
        }

        /// <summary>
        /// Invalidates the current user's refresh token for the given browser session. Requires a valid access token.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        public virtual async Task<ActionResult> Logout(string browserId)
        {
            long userId = _authenticationService.GetCurrentUserId();
            await _jwtAuthManagerService.LogoutAsync(browserId, userId); // If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user

            return Ok();
        }

        /// <summary>
        /// Like <see cref="Logout"/>, and additionally clears the auth HttpOnly cookies. Requires a valid access token.
        /// </summary>
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
        /// Refreshes the access token using the refresh token supplied in the request body, returning a new
        /// access + refresh token pair. Anonymous — the refresh token is itself the credential.
        /// </summary>
        // No [Authorize] here: the access token's lifetime is intentionally not validated at the filter level —
        // it is re-validated (without the lifetime check) deeper in the method.
        [HttpPost]
        public virtual async Task<AuthResultDTO> RefreshTokenWithHeaders(RefreshTokenRequestDTO request)
        {
            return await _securityServiceBase.RefreshTokenWithHeaders(request);
        }

        /// <summary>
        /// Refreshes the access token using the refresh token stored in an HttpOnly cookie. POST (not GET)
        /// because it mutates server state — it rotates the single-use refresh token. A safe/idempotent GET
        /// was cacheable, so browsers replayed a stale "logged-in" body on back/forward navigations (phantom
        /// dashboard after logout); POST is never cached, and the controller-level no-store is belt-and-braces.
        /// </summary>
        [HttpPost]
        public virtual async Task<AuthResultWithCookiesDTO> RefreshTokenWithCookies([FromQuery] string browserId)
        {
            return await _securityServiceBase.RefreshTokenWithCookies(browserId);
        }

        #endregion

        #region User

        /// <summary>
        /// Returns the authenticated user's base profile (id, email, core fields). Requires a valid access token.
        /// </summary>
        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public virtual async Task<UserBaseDTO> GetCurrentUserBase()
        {
            return await _authenticationService.GetCurrentUserBaseDTO<TUser>();
        }

        /// <summary>
        /// Returns the permission codes granted to the authenticated user via their roles. Requires a valid access token.
        /// </summary>
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
