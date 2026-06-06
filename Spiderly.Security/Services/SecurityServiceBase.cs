using FluentValidation;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.ValidationRules;
using Spiderly.Shared;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides business logic for security-related operations, including authentication, registration,
    /// and token management. It leverages various services like JWT authentication,
    /// email sending, and data access through Entity Framework Core.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    /// <typeparam name="TUserExternalLogin">The entity linking a user to an external provider login, implementing <see cref="IUserExternalLogin"/>.</typeparam>
    public class SecurityServiceBase<TUser, TUserExternalLogin>
        where TUser : class, IUser, new()
        where TUserExternalLogin : class, IUserExternalLogin, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly AuthenticationService _authenticationService;
        private readonly IEmailingService _emailingService;
        private readonly IWebHostEnvironment _environment;
        private readonly IStringLocalizer _localizer;
        private readonly AuthPolicyOptions _authPolicySettings;
        private readonly IExternalAuthProviderRegistry _externalAuthProviderRegistry;
        private readonly ExternalAuthCodeFlow _externalAuthCodeFlow;
        private readonly IDataProtector _externalLoginProtector;
        private readonly IDataProtector _externalLoginNonceProtector;
        private readonly string _frontendUrl;

        public SecurityServiceBase(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManagerService,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            IWebHostEnvironment environment,
            IStringLocalizer localizer,
            IOptions<AuthPolicyOptions> authPolicyOptions,
            IExternalAuthProviderRegistry externalAuthProviderRegistry,
            ExternalAuthCodeFlow externalAuthCodeFlow,
            IDataProtectionProvider dataProtectionProvider,
            IOptions<Spiderly.Shared.Settings> sharedSettings
        )
        {
            _context = context;
            _jwtAuthManagerService = jwtAuthManagerService;
            _emailingService = emailingService;
            _authenticationService = authenticationService;
            _environment = environment;
            _localizer = localizer;
            _authPolicySettings = authPolicyOptions.Value;
            _externalAuthProviderRegistry = externalAuthProviderRegistry;
            _externalAuthCodeFlow = externalAuthCodeFlow;
            _externalLoginProtector = dataProtectionProvider.CreateProtector("Spiderly.Security.ExternalLogin");
            _externalLoginNonceProtector = dataProtectionProvider.CreateProtector("Spiderly.Security.ExternalLoginNonce");
            _frontendUrl = sharedSettings.Value.FrontendUrl;
        }

        #region Authentication

        #region Login

        public virtual async Task<SendLoginVerificationEmailResultDTO> SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            new LoginDTOValidationRules().ValidateAndThrow(loginDTO);

            TUser user = await Authenticate(loginDTO);

            string userEmail;

            if (user == null)
            {
                if (_authPolicySettings.OnlyAdminCanAddUsers)
                    throw new BusinessException(_localizer["AuthenticationEmailDoesNotExistException"]);

                userEmail = loginDTO.Email;
            }
            else
            {
                userEmail = user.Email;
            }

            // Per-recipient guard against inbox-flooding / email-quota abuse on this (storefront-and-admin
            // shared) endpoint — see IsLoginVerificationSendBlockedAsync. Only applied when emailing is
            // configured, i.e. a real email would actually be sent; the dev inline-code path sends none, so
            // local/e2e logins are unaffected. Silently report success — never an error — so the endpoint
            // leaks neither whether the address exists nor that it's being targeted.
            if (_emailingService.IsConfigured()
                && await _jwtAuthManagerService.IsLoginVerificationSendBlockedAsync(userEmail))
            {
                return new SendLoginVerificationEmailResultDTO { Message = string.Empty };
            }

            string verificationCode = await _jwtAuthManagerService.GenerateAndSaveLoginVerificationCodeAsync(userEmail, loginDTO.BrowserId);

            if (ShouldShowVerificationCodeInNotification())
            {
                return new SendLoginVerificationEmailResultDTO
                {
                    Message = _localizer["VerificationCodeDevelopmentMode", verificationCode],
                    VerificationCode = verificationCode
                };
            }

            EmailVerifyUIDTO emailTemplate = CreateLoginEmailTemplate(verificationCode);

            try
            {
                await _emailingService.SendVerificationEmailAsync(userEmail, emailTemplate);
            }
            catch (Exception)
            {
                // We didn't send email, set all verification tokens invalid then
                await _jwtAuthManagerService.RemoveLoginVerificationTokensByEmailAsync(userEmail);
                throw;
            }

            return new SendLoginVerificationEmailResultDTO
            {
                Message = string.Empty
            };
        }

        public virtual EmailVerifyUIDTO CreateLoginEmailTemplate(string verificationCode)
        {
            return new EmailVerifyUIDTO
            {
                Subject = _localizer["EmailAccountVerificationTitle"],
                Body = verificationCode
            };
        }

        public virtual async Task<AuthResultDTO> Login(VerificationTokenRequestDTO verificationRequestDTO)
        {
            new VerificationTokenRequestDTOValidationRules().ValidateAndThrow(verificationRequestDTO);

            // Can not be null, if its null it already has thrown
            LoginVerificationTokenDTO loginVerificationTokenDTO = await _jwtAuthManagerService.ValidateAndGetLoginVerificationTokenDTOAsync(
                verificationRequestDTO.VerificationCode, verificationRequestDTO.BrowserId, verificationRequestDTO.Email);

            return await _context.WithTransactionAsync(async () =>
            {
                // Check if the user already exists in the database
                TUser user = await GetUserByEmailAsync(loginVerificationTokenDTO.Email);
                DbSet<TUser> userDbSet = _context.DbSet<TUser>();

                if (user == null)
                {
                    if (_authPolicySettings.OnlyAdminCanAddUsers)
                        throw new BusinessException(_localizer["AuthenticationEmailDoesNotExistException"]);

                    user = new TUser
                    {
                        Email = loginVerificationTokenDTO.Email,
                    };

                    await userDbSet.AddAsync(user);
                    await _context.SaveChangesAsync(); // Adding the new user which is logged in first time
                }
                else
                {
                    if (user.IsDisabled == true)
                        throw new BusinessException(_localizer["DisabledAccountException"]);
                }

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, loginVerificationTokenDTO.BrowserId);

                AuthResultDTO authResultDTO = new AuthResultDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    AccessToken = jwtAuthResultDTO.AccessTokenDTO.TokenString,
                    AccessTokenExpiresAt = jwtAuthResultDTO.AccessTokenDTO.ExpiresAt,
                    RefreshToken = jwtAuthResultDTO.RefreshTokenDTO.TokenString,
                };

                await OnAfterLogin(authResultDTO);

                return authResultDTO;
            });
        }

        /// <summary>
        /// Step 1 of the server-side external-login flow (Option B2): builds the provider's authorize URL
        /// (PKCE + nonce + state) and returns a Data-Protection-signed state blob the caller stores in a
        /// short-lived cookie. <paramref name="redirectUri"/> is the backend callback (must be registered with the provider).
        /// </summary>
        public virtual async Task<(string AuthorizeUrl, string ProtectedState)> BeginExternalLoginAsync(string provider, string returnUrl, string browserId, string redirectUri)
        {
            ExternalProviderConfig config = _externalAuthCodeFlow.GetConfig(provider); // throws ExternalProviderNotConfigured if absent

            // Sanitize here (before it's signed into the state) so the callback redirect can't be pointed off-site.
            returnUrl = SanitizeReturnUrl(returnUrl);

            string state = GenerateUrlToken();
            string nonce = GenerateUrlToken();
            string codeVerifier = GenerateUrlToken(64);
            string codeChallenge = ComputeS256Challenge(codeVerifier);

            string authorizeUrl = await _externalAuthCodeFlow.BuildAuthorizeUrlAsync(config, state, nonce, codeChallenge, redirectUri);

            ExternalLoginState payload = new()
            {
                Provider = provider,
                State = state,
                Nonce = nonce,
                CodeVerifier = codeVerifier,
                ReturnUrl = returnUrl,
                BrowserId = browserId,
                RedirectUri = redirectUri,
            };

            string protectedState = _externalLoginProtector.Protect(JsonSerializer.Serialize(payload));

            return (authorizeUrl, protectedState);
        }

        /// <summary>
        /// Step 2: validates the signed state, exchanges the code for an id token server-side (with the client
        /// secret), validates the id token + nonce, resolves/links the user, issues the session as HttpOnly
        /// cookies, and returns the original returnUrl for the caller to redirect to.
        /// </summary>
        public virtual async Task<string> CompleteExternalLoginAsync(string code, string state, string protectedState)
        {
            ExternalLoginState payload = UnprotectExternalLoginState(protectedState);

            if (payload == null || string.IsNullOrWhiteSpace(state) || payload.State != state)
                throw new SecurityViolationException("External login state validation failed.");

            if (string.IsNullOrWhiteSpace(code))
                throw new BusinessException(_localizer["ExternalProviderNotConfiguredException"], ApiErrorCodes.ExternalProviderNotConfigured);

            ExternalProviderConfig config = _externalAuthCodeFlow.GetConfig(payload.Provider);

            string idToken = await _externalAuthCodeFlow.ExchangeCodeForIdTokenAsync(config, code, payload.CodeVerifier, payload.RedirectUri);

            ExternalIdentity externalIdentity = await _externalAuthProviderRegistry.Get(payload.Provider).ValidateAsync(idToken);

            // Nonce binding: the id token must echo the nonce we generated (defeats token injection/replay).
            string tokenNonce = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().ReadJsonWebToken(idToken).TryGetClaim("nonce", out Claim nonceClaim) ? nonceClaim.Value : null;
            if (tokenNonce != payload.Nonce)
                throw new SecurityViolationException("External login nonce mismatch.");

            return await _context.WithTransactionAsync(async () =>
            {
                TUser user = await ResolveExternalUser(externalIdentity);

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, payload.BrowserId);

                AuthResultWithCookiesDTO authResultWithCookiesDTO = new()
                {
                    userId = user.Id,
                    email = user.Email,
                    accessTokenExpiresAt = jwtAuthResultDTO.AccessTokenDTO.ExpiresAt,
                };

                _authenticationService.SetRefreshTokenCookie(jwtAuthResultDTO.RefreshTokenDTO.TokenString);
                _authenticationService.SetAccessTokenCookie(jwtAuthResultDTO.AccessTokenDTO.TokenString);
                _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

                return payload.ReturnUrl;
            });
        }

        /// <summary>
        /// Open-redirect guard for the external-login callback: a return URL is honored only when its origin
        /// (scheme + host + port) exactly matches the configured frontend; anything else falls back to the
        /// frontend root. A prefix/<c>StartsWith</c> check is deliberately avoided — it is bypassable by a
        /// look-alike host such as <c>https://app.example.com.evil.com</c>, which starts with the allowed origin
        /// but is a different site. Never echoes an unvalidated caller-supplied URL.
        /// </summary>
        /// <summary>
        /// Builds the redirect used when the server-side external-login callback can't complete (e.g. the state
        /// cookie expired because the user lingered on the provider's account picker). Always targets the
        /// configured frontend origin — never an untrusted return URL — with an <c>externalAuthError</c> hint
        /// the SPA surfaces as a friendly message. <paramref name="code"/> is <c>expired</c> (took too long) or
        /// <c>failed</c> (invalid state/nonce, failed code exchange, or denied consent).
        /// </summary>
        public virtual string BuildExternalAuthErrorRedirectUrl(string code)
        {
            return $"{_frontendUrl.TrimEnd('/')}/?externalAuthError={Uri.EscapeDataString(code)}";
        }

        private string SanitizeReturnUrl(string returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Uri.TryCreate(returnUrl, UriKind.Absolute, out Uri target) &&
                Uri.TryCreate(_frontendUrl, UriKind.Absolute, out Uri allowed) &&
                Uri.Compare(target, allowed, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return returnUrl;
            }

            return _frontendUrl;
        }

        private static string GenerateUrlToken(int byteLength = 32) => Base64UrlEncode(RandomNumberGenerator.GetBytes(byteLength));

        private static string ComputeS256Challenge(string codeVerifier) => Base64UrlEncode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));

        private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private ExternalLoginState UnprotectExternalLoginState(string protectedState)
        {
            if (string.IsNullOrWhiteSpace(protectedState))
                return null;

            try
            {
                return JsonSerializer.Deserialize<ExternalLoginState>(_externalLoginProtector.Unprotect(protectedState));
            }
            catch
            {
                return null; // tampered / expired / wrong key → treated as no state
            }
        }

        private sealed class ExternalLoginState
        {
            public string Provider { get; set; }
            public string State { get; set; }
            public string Nonce { get; set; }
            public string CodeVerifier { get; set; }
            public string ReturnUrl { get; set; }
            public string BrowserId { get; set; }
            public string RedirectUri { get; set; }
        }

        private const int ExternalLoginNonceLifetimeMinutes = 15;

        /// <summary>
        /// Issues a one-time nonce for the client-side (GIS / id-token) external-login flow. Returns the raw
        /// nonce — the SPA passes it to the provider's sign-in call so it is echoed into the id token's
        /// <c>nonce</c> claim — and a Data-Protection-signed copy the caller stores in a short-lived HttpOnly
        /// cookie. <see cref="VerifyExternalLoginNonce"/> then checks the echoed claim against the cookie,
        /// binding the login to this browser and making the id token single-use (replay / login-CSRF guard).
        /// </summary>
        public virtual (string Nonce, string ProtectedNonce) CreateExternalLoginNonce()
        {
            string nonce = GenerateUrlToken();

            ExternalLoginNonce payload = new()
            {
                Nonce = nonce,
                ExpiresAt = DateTime.UtcNow.AddMinutes(ExternalLoginNonceLifetimeMinutes),
            };

            string protectedNonce = _externalLoginNonceProtector.Protect(JsonSerializer.Serialize(payload));

            return (nonce, protectedNonce);
        }

        /// <summary>
        /// Verifies the id token echoes the server-issued, browser-bound nonce (see
        /// <see cref="CreateExternalLoginNonce"/>). Throws <see cref="SecurityViolationException"/> when the
        /// nonce is missing, tampered, expired, or doesn't match the token's <c>nonce</c> claim. This is the
        /// id-token path's equivalent of the nonce check the B2 code flow does in <see cref="CompleteExternalLoginAsync"/>.
        /// </summary>
        private void VerifyExternalLoginNonce(string idToken, string protectedNonce)
        {
            ExternalLoginNonce payload = null;

            if (string.IsNullOrWhiteSpace(protectedNonce) == false)
            {
                try
                {
                    payload = JsonSerializer.Deserialize<ExternalLoginNonce>(_externalLoginNonceProtector.Unprotect(protectedNonce));
                }
                catch
                {
                    payload = null; // tampered / expired / wrong key → treated as no nonce
                }
            }

            if (payload == null || payload.ExpiresAt < DateTime.UtcNow)
                throw new SecurityViolationException("External login nonce is missing or expired.");

            string tokenNonce = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler()
                .ReadJsonWebToken(idToken)
                .TryGetClaim("nonce", out Claim nonceClaim) ? nonceClaim.Value : null;

            if (string.IsNullOrWhiteSpace(tokenNonce) || tokenNonce != payload.Nonce)
                throw new SecurityViolationException("External login nonce mismatch.");
        }

        private sealed class ExternalLoginNonce
        {
            public string Nonce { get; set; }
            public DateTime ExpiresAt { get; set; }
        }

        public virtual List<ExternalProviderPublicDTO> GetExternalProviders()
        {
            return _externalAuthProviderRegistry.GetPublicConfigs()
                .Select(x => new ExternalProviderPublicDTO
                {
                    Code = x.Code,
                    Authority = x.Authority,
                    ClientId = x.ClientId,
                    Label = x.Label,
                })
                .ToList();
        }

        public virtual async Task<AuthResultDTO> LoginExternal(ExternalProviderDTO externalProviderDTO, string protectedNonce)
        {
            // Localized guard here (the service has the localizer); the registry's own throw is an internal safety net.
            if (_externalAuthProviderRegistry.IsConfigured(externalProviderDTO.Provider) == false)
                throw new BusinessException(_localizer["ExternalProviderNotConfiguredException"], ApiErrorCodes.ExternalProviderNotConfigured);

            IExternalAuthProvider provider = _externalAuthProviderRegistry.Get(externalProviderDTO.Provider);

            ExternalIdentity externalIdentity = await provider.ValidateAsync(externalProviderDTO.IdToken);

            // Replay / login-CSRF guard: the id token must echo the server-issued, browser-bound nonce.
            VerifyExternalLoginNonce(externalProviderDTO.IdToken, protectedNonce);

            return await _context.WithTransactionAsync(async () =>
            {
                TUser user = await ResolveExternalUser(externalIdentity);

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, externalProviderDTO.BrowserId);

                AuthResultDTO authResultDTO = new AuthResultDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    AccessToken = jwtAuthResultDTO.AccessTokenDTO.TokenString,
                    AccessTokenExpiresAt = jwtAuthResultDTO.AccessTokenDTO.ExpiresAt,
                    RefreshToken = jwtAuthResultDTO.RefreshTokenDTO.TokenString,
                };

                await OnAfterLogin(authResultDTO);

                return authResultDTO;
            });
        }

        public virtual async Task OnAfterLogin(AuthResultDTO authResultDTO) { }

        public virtual async Task<AuthResultWithCookiesDTO> LoginWithCookies(VerificationTokenRequestDTO verificationRequestDTO)
        {
            AuthResultDTO authResultDTO = await Login(verificationRequestDTO);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        public virtual async Task<AuthResultWithCookiesDTO> LoginExternalWithCookies(ExternalProviderDTO externalProviderDTO, string protectedNonce)
        {
            AuthResultDTO authResultDTO = await LoginExternal(externalProviderDTO, protectedNonce);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        #endregion

        #region Helpers

        public virtual async Task<AuthResultDTO> RefreshToken(RefreshTokenRequestDTO refreshTokenRequestDTO, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRequestDTO.RefreshToken))
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]); // It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            long? userIdFromAccessToken = null;

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                List<Claim> claims = await _jwtAuthManagerService.GetClaimsForTheAccessTokenAsync(refreshTokenRequestDTO, accessToken);
                // Raw wire claim from JwtSecurityToken.Claims — the bearer middleware's inbound map (sub → NameIdentifier) has not been applied here.
                userIdFromAccessToken = long.Parse(claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Sub)?.Value);
            }

            // When access token cookie expired (browser deleted it), RefreshAsync derives user ID from refresh token storage.
            JwtAuthResultDTO jwtResult = await _jwtAuthManagerService.RefreshAsync(refreshTokenRequestDTO, userIdFromAccessToken);

            string emailFromTheDb = await GetUserEmailByIdAsync(jwtResult.UserId);

            return new AuthResultDTO
            {
                UserId = jwtResult.UserId, // Here it will always be user, if there is not, it will break earlier
                Email = emailFromTheDb,
                AccessToken = jwtResult.AccessTokenDTO.TokenString,
                AccessTokenExpiresAt = jwtResult.AccessTokenDTO.ExpiresAt,
                RefreshToken = jwtResult.RefreshTokenDTO.TokenString
            };
        }

        public virtual async Task<AuthResultDTO> RefreshTokenWithHeaders(RefreshTokenRequestDTO refreshTokenRequestDTO)
        {
            string accessToken = _authenticationService.GetAccessTokenFromHeader();
            return await RefreshToken(refreshTokenRequestDTO, accessToken);
        }

        public virtual async Task<AuthResultWithCookiesDTO> RefreshTokenWithCookies(string browserId)
        {
            string refreshToken = _authenticationService.GetRefreshTokenFromCookie();
            string accessToken = _authenticationService.GetAccessTokenFromCookie();

            RefreshTokenRequestDTO refreshTokenRequestDTO = new RefreshTokenRequestDTO
            {
                RefreshToken = refreshToken,
                BrowserId = browserId
            };

            AuthResultDTO authResultDTO = await RefreshToken(refreshTokenRequestDTO, accessToken);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        public virtual async Task<string> GetUserEmailByIdAsync(long id)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Id == id).Select(x => x.Email).SingleOrDefaultAsync();
            });
        }

        public virtual async Task<TUser> GetUserByEmailAsync(string email)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Email == email).SingleOrDefaultAsync();
            });
        }

        /// <summary>
        /// Resolves the application user for a validated external identity. Links by the provider's stable
        /// subject first (rename-proof); for a first-time login it auto-links to an existing user with the
        /// same provider-verified email, or creates one — gated by <see cref="AuthPolicyOptions.AutoLinkByVerifiedEmail"/>
        /// and <see cref="AuthPolicyOptions.OnlyAdminCanAddUsers"/>. An unverified email is always rejected.
        /// Runs inside the caller's transaction.
        /// </summary>
        protected virtual async Task<TUser> ResolveExternalUser(ExternalIdentity externalIdentity)
        {
            DbSet<TUser> userDbSet = _context.DbSet<TUser>();
            DbSet<TUserExternalLogin> loginDbSet = _context.DbSet<TUserExternalLogin>();

            // 1. Already linked by (provider, subject) — the stable, rename-proof key.
            long? linkedUserId = await loginDbSet
                .Where(x => x.Provider == externalIdentity.Provider && x.ProviderKey == externalIdentity.Subject)
                .Select(x => (long?)x.UserId)
                .SingleOrDefaultAsync();

            if (linkedUserId != null)
            {
                TUser linkedUser = await userDbSet.Where(x => x.Id == linkedUserId.Value).SingleOrDefaultAsync();

                if (linkedUser == null)
                    throw new BusinessException(_localizer["AuthenticationEmailDoesNotExistException"]);

                if (linkedUser.IsDisabled == true)
                    throw new BusinessException(_localizer["DisabledAccountException"]);

                return linkedUser;
            }

            // 2. Not linked yet — auto-provisioning keys the account on the email, so the provider must
            // return one. Some providers (e.g. Facebook) can complete a login with no email (the user
            // declined the email permission, or a phone-only account).
            if (string.IsNullOrWhiteSpace(externalIdentity.Email))
                throw new BusinessException(_localizer["ExternalEmailMissingException"], ApiErrorCodes.ExternalEmailMissing);

            // 3. An email is present — it must be provider-verified (or the provider trusted via
            // TrustEmailVerified) before we link or create an account from it.
            if (externalIdentity.EmailVerified != true)
                throw new BusinessException(_localizer["ExternalEmailNotVerifiedException"], ApiErrorCodes.EmailNotVerified);

            TUser user = await userDbSet.Where(x => x.Email == externalIdentity.Email).SingleOrDefaultAsync();

            if (user != null)
            {
                if (user.IsDisabled == true)
                    throw new BusinessException(_localizer["DisabledAccountException"]);

                if (_authPolicySettings.AutoLinkByVerifiedEmail == false)
                    throw new BusinessException(_localizer["ExternalLinkingRequiresSignInException"]);
            }
            else
            {
                if (_authPolicySettings.OnlyAdminCanAddUsers)
                    throw new BusinessException(_localizer["AuthenticationEmailDoesNotExistException"]);

                user = new TUser { Email = externalIdentity.Email };
                await userDbSet.AddAsync(user);
                await _context.SaveChangesAsync(); // Persist so the new user has an Id to link against.
            }

            TUserExternalLogin externalLogin = new()
            {
                UserId = user.Id,
                Provider = externalIdentity.Provider,
                ProviderKey = externalIdentity.Subject,
            };
            await loginDbSet.AddAsync(externalLogin);
            await _context.SaveChangesAsync();

            return user;
        }

        private async Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokens(long userId, string browserId)
        {
            string ipAddress = _authenticationService.GetIPAddress();

            JwtAuthResultDTO jwtAuthResult = await _jwtAuthManagerService.GenerateAccessAndRefreshTokensAsync(userId, ipAddress, browserId);

            return jwtAuthResult;
        }

        private async Task<TUser> Authenticate(LoginDTO loginDTO)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                TUser currentUser = await _context.DbSet<TUser>()
                    .Where(x => x.Email == loginDTO.Email)
                    .SingleOrDefaultAsync();

                if (currentUser == null)
                    return null;

                if (currentUser.IsDisabled == true)
                    throw new BusinessException(_localizer["DisabledAccountException"]);

                return currentUser;
            });
        }

        private bool ShouldShowVerificationCodeInNotification()
        {
            bool isEmailingConfigured = _emailingService.IsConfigured();
            bool isDevelopment = _environment.IsDevelopment();

            return isDevelopment && !isEmailingConfigured;
        }

        #endregion

        #endregion

    }
}
