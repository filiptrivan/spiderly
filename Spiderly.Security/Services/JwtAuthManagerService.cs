using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Shared;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Collections.Immutable;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Manages JWT (JSON Web Token) authentication and refresh token functionalities.
    /// It handles the generation, validation, and storage of access and refresh tokens,
    /// as well as verification tokens for login and registration processes.
    /// </summary>
    public class JwtAuthManagerService : IJwtAuthManager, IDisposable
    {
        private readonly ITokenStorage<RefreshTokenDTO> _usersRefreshTokens;
        private readonly ITokenStorage<LoginVerificationTokenDTO> _usersLoginVerificationTokens;
        private readonly IStringLocalizer _localizer;
        private readonly JwtOptions _jwtSettings;
        private readonly AuthPolicyOptions _authPolicySettings;
        private readonly SigningCredentials _signingCredentials;
        private readonly TokenValidationParameters _accessTokenValidationParameters;

        private static readonly Random Random = new();
        private static readonly JsonWebTokenHandler _jsonWebTokenHandler = new();

        public JwtAuthManagerService(
            ITokenStorage<RefreshTokenDTO> refreshTokenStorage,
            ITokenStorage<LoginVerificationTokenDTO> loginVerificationTokenStorage,
            IStringLocalizer localizer,
            IOptions<JwtOptions> jwtOptions,
            IOptions<AuthPolicyOptions> authPolicyOptions)
        {
            _usersRefreshTokens = refreshTokenStorage;
            _usersLoginVerificationTokens = loginVerificationTokenStorage;
            _localizer = localizer;
            _jwtSettings = jwtOptions.Value;
            _authPolicySettings = authPolicyOptions.Value;

            // Key/issuer/audience are fixed for the service lifetime, so build the signing credentials and
            // validation parameters once instead of per token issue/validate (both on the auth hot path).
            SymmetricSecurityKey signingKey = new(Encoding.UTF8.GetBytes(_jwtSettings.JwtKey));
            _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            _accessTokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.JwtIssuer,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ValidAudience = _jwtSettings.JwtAudience,
                ValidateAudience = true,
                ValidateLifetime = false, // Refresh deliberately reads claims from a possibly-expired access token
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // Pin HS256 to block alg-downgrade (e.g. "none")
                ClockSkew = TimeSpan.FromMinutes(_jwtSettings.ClockSkewMinutes),
            };
        }

        public virtual async Task<IImmutableDictionary<string, RefreshTokenDTO>> GetUsersRefreshTokensReadOnlyDictionaryAsync()
        {
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> tokens = await _usersRefreshTokens.GetAllAsync();
            return tokens.ToImmutableDictionary();
        }

        public virtual async Task<IImmutableDictionary<string, LoginVerificationTokenDTO>> GetUsersLoginVerificationTokensReadOnlyDictionaryAsync()
        {
            IEnumerable<KeyValuePair<string, LoginVerificationTokenDTO>> tokens = await _usersLoginVerificationTokens.GetAllAsync();
            return tokens.ToImmutableDictionary();
        }

        #region Refresh

        /// <summary>
        /// 1. Stole refresh but doesn't have access - we validate if he has access
        /// 2. Stole a refresh from one user, he has his own valid access - we log both of them out because they have different emails
        /// 3. Stole access but no refresh
        /// 4. Stole both - we can't do anything to him, we only try to stop him if he's on a different ip address
        /// </summary>
        /// <param name="userIdFromAccessToken">
        /// User ID extracted from the (possibly expired) access token. Null when the access token cookie
        /// has expired and been deleted by the browser — in that case the user ID is derived from the
        /// refresh token storage instead.
        /// </param>
        /// <param name="request">The refresh token request containing the refresh token string and browser ID.</param>
        public virtual async Task<JwtAuthResultDTO> RefreshAsync(RefreshTokenRequestDTO request, long? userIdFromAccessToken)
        {
            await RemoveExpiredRefreshTokensAsync();

            // A token this service has never issued, or one already past the end of its grace window, lands
            // here as null. Both are genuinely dead sessions and belong on the login page.
            // !: callers reject a missing refresh token before calling (SecurityServiceBase.RefreshToken's IsNullOrWhiteSpace guard)
            RefreshTokenDTO? existingRefreshToken = await _usersRefreshTokens.TryGetValueAsync(request.RefreshToken!);
            if (existingRefreshToken == null)
            {
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]);
            }

            long userId = userIdFromAccessToken ?? existingRefreshToken.UserId;

            // We can assume that userEmailFromAccessToken and refreshTokenEmail are the same, because if they are not anyway we will go through and delete everything
            await RemoveTokensForMoreThenAllowedBrowsersAsync(userId);

            // Unauthenticating both user, this could happen if someone stoled access token (aleksa.trivan), and has own valid refresh token (filip.trivan), he could indefinedly generate access tokens for the (aleksa.trivan) then
            // This is not solving this problem (hacker can not change claims in the jwt token): https://stackoverflow.com/questions/27301557/if-you-can-decode-jwt-how-are-they-secure we are doing that with Decoding JWT token.
            // It is not posible for the user to change the email of the refresh token, even if it is, if the user change the email in the refresh token, it doesn't matter, we will find based on the refresh token code not email
            if (userIdFromAccessToken.HasValue && existingRefreshToken.UserId != userIdFromAccessToken.Value) // Could happen if someone gives me access and refresh for different users, i don't know which of these he stole so i unauthenticate both
            {
                await RemoveRefreshTokenByUserIdAsync(existingRefreshToken.UserId);
                await RemoveRefreshTokenByUserIdAsync(userIdFromAccessToken.Value);
                throw new SecurityViolationException("The user id can't be different in refresh and access token.");
            }
            if (_authPolicySettings.AllowTheUseOfAppWithDifferentIpAddresses == false && await IsRefreshTokenWithNewIpAddressAsync(existingRefreshToken.UserId, existingRefreshToken.IpAddress))
            {
                // Future idea: track the user's frequently-used devices as a "device family", flag a suspicious
                // one by some heuristic, and require multi-factor auth on it — if the client opted into MFA at all.
                await RemoveRefreshTokenByUserIdAsync(existingRefreshToken.UserId); // Don't need to delete for userDTO also, because we already did that
                throw new SecurityTokenException(_localizer["TwoDifferentIpAddressesRefreshException"]);
            }

            // Presenting a superseded token means this request was composed before the rotation's cookie came
            // back — routine with a second tab, or with another app under the same cookie domain. Answer with
            // the successor instead of rotating again: a second rotation would leave the client that won the
            // race holding a token that is itself superseded, and the two would keep chasing each other.
            if (existingRefreshToken.SupersededByTokenString != null)
            {
                RefreshTokenDTO successorRefreshToken = await ResolveSuccessorRefreshTokenAsync(existingRefreshToken);

                return new JwtAuthResultDTO
                {
                    UserId = userId,
                    AccessTokenDTO = GenerateAccessToken(GenerateClaims(userId)), // the caller refreshed because its own was expiring
                    RefreshTokenDTO = successorRefreshToken,
                };
            }

            return await GenerateAccessAndRefreshTokensAsync(userId, existingRefreshToken.IpAddress, request.BrowserId); // need to recover the original claims
        }

        /// <summary>
        /// Walks a superseded token to the live token at the end of its chain. A client that slept through
        /// several rotations holds a token some hops back, so one step is not always enough. The chain is
        /// bounded rather than trusted: a cycle in storage would otherwise hang the request.
        /// </summary>
        private async Task<RefreshTokenDTO> ResolveSuccessorRefreshTokenAsync(RefreshTokenDTO supersededRefreshToken)
        {
            const int maxChainLength = 10;

            RefreshTokenDTO currentRefreshToken = supersededRefreshToken;

            for (int hop = 0; hop < maxChainLength; hop++)
            {
                RefreshTokenDTO? successorRefreshToken = await _usersRefreshTokens.TryGetValueAsync(currentRefreshToken.SupersededByTokenString!);

                // The successor is gone (logged out, revoked, or expired), so the session it pointed at no
                // longer exists — the same dead end as an unknown token.
                if (successorRefreshToken == null)
                    throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]);

                if (successorRefreshToken.SupersededByTokenString == null)
                    return successorRefreshToken;

                currentRefreshToken = successorRefreshToken;
            }

            throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]);
        }

        protected readonly SemaphoreSlim _generateAccessAndRefreshTokensLock = new(1, 1);

        /// <summary>
        /// Password and verificationExpiration (minutes) are only needed if we are registering the account, for email verification
        /// </summary>
        public virtual async Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokensAsync(long userId, string? ipAddress, string? browserId)
        {
            List<Claim> claims = GenerateClaims(userId);

            AccessTokenDTO accessTokenDTO = GenerateAccessToken(claims);

            RefreshTokenDTO refreshTokenDTO = new RefreshTokenDTO
            {
                UserId = userId,
                IpAddress = ipAddress,
                BrowserId = browserId,
                TokenString = GenerateRandomTokenString(),
                ExpiresAt = DateTime.UtcNow.AddMinutes(_authPolicySettings.RefreshTokenExpiration),
            };

            await _generateAccessAndRefreshTokensLock.WaitAsync();
            try
            {
                await SupersedeLastRefreshTokenFromTheSameBrowserAndUserIdAsync(browserId, userId, refreshTokenDTO.TokenString); // userId also because the hacker could manipulate browserId, but he can't userId

                // It will always generate new token,
                // it is beneficial if the user open the application from different devices
                // if the user open the application on the multiple tabs in the same browser, we are working with the local storage so it will not make the difference
                await _usersRefreshTokens.AddOrUpdateAsync(refreshTokenDTO.TokenString, refreshTokenDTO);
            }
            finally
            {
                _generateAccessAndRefreshTokensLock.Release();
            }

            return new JwtAuthResultDTO
            {
                UserId = userId,
                AccessTokenDTO = accessTokenDTO,
                RefreshTokenDTO = refreshTokenDTO,
            };
        }

        /// <summary>
        /// Emits the JWT user id as the RFC 7519 <c>"sub"</c> claim. Request-time readers see it as
        /// <see cref="ClaimTypes.NameIdentifier"/> after the bearer middleware's default inbound map;
        /// code reading raw <see cref="JsonWebToken"/> claims (e.g. the refresh flow) must look up
        /// <see cref="JwtRegisteredClaimNames.Sub"/> instead, because no middleware mapping applies there.
        /// Also stamps the <see cref="PrincipalClaims.PrincipalKind"/> claim: the email-login flow only ever
        /// issues tokens for the built-in human principal, so the kind is always <see cref="PrincipalKinds.User"/>.
        /// This is what lets authorization dispatch to the right principal kind once an app has more than one.
        /// </summary>
        public virtual List<Claim> GenerateClaims(long userId)
        {
            return new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(PrincipalClaims.PrincipalKind, PrincipalKinds.User),
            };
        }

        #region Helpers

        private AccessTokenDTO GenerateAccessToken(List<Claim> claims, int? verificationExpiration = null)
        {
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(verificationExpiration ?? _authPolicySettings.AccessTokenExpiration);

            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtSettings.JwtIssuer,
                Audience = _jwtSettings.JwtAudience,
                Expires = expiresAt,
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = _signingCredentials,
            };

            string accessToken = _jsonWebTokenHandler.CreateToken(tokenDescriptor);

            return new AccessTokenDTO
            {
                TokenString = accessToken,
                ExpiresAt = expiresAt,
            };
        }

        public virtual async Task<List<Claim>> GetClaimsForTheAccessTokenAsync(RefreshTokenRequestDTO request, string accessToken)
        {
            List<Claim> principalClaims;

            try
            {
                JsonWebToken jwtToken = await ValidateJwtTokenAsync(accessToken); // We are not validating the jwt token here, we are just reading claims so we don't need to go to the database
                principalClaims = jwtToken.Claims.ToList();
            }
            catch (Exception)
            {
                // !: reached only from the refresh flow, which already rejected a missing refresh token
                await _usersRefreshTokens.TryRemoveAsync(request.RefreshToken!); // If the user hadn't access token but trying somehow to do something ilegal, remove the passed refresh also
                throw;
            }

            return principalClaims; // It's not possible to return null, if there is no exception it will return, if there is the catch block will throw
        }

        private async Task<JsonWebToken> ValidateJwtTokenAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]); // It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            TokenValidationResult result = await _jsonWebTokenHandler.ValidateTokenAsync(accessToken, _accessTokenValidationParameters);

            if (result.IsValid == false) // ValidateTokenAsync reports failure via IsValid instead of throwing; the flow expects an exception
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]);

            if (result.SecurityToken is not JsonWebToken jwtToken)
                throw new InvalidOperationException("Expected a JsonWebToken from a successful validation result.");

            return jwtToken;
        }

        /// <summary>
        /// If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user
        /// </summary>
        public virtual async Task LogoutAsync(string? browserId, long userId)
        {
            bool foundTheUser = await RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(browserId, userId);

            if (foundTheUser == false)
            {
                await RemoveRefreshTokenByUserIdAsync(userId);
            }
        }

        /// <summary>
        /// Retires the live refresh token of this (user, browser) in favour of
        /// <paramref name="successorTokenString"/>. With a grace window configured the old token is kept,
        /// pointing at its successor and expiring at the end of the window, so a request that was already in
        /// flight with it is answered rather than rejected; at zero it is deleted outright. Tokens that are
        /// themselves already superseded are left to expire on their own, so a chain is never broken from
        /// behind. See <see cref="AuthPolicyOptions.RefreshTokenGraceSeconds"/>.
        /// </summary>
        private async Task SupersedeLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string? browserId, long userId, string successorTokenString)
        {
            // TODO Log if the email or browser id is null

            // Normally exactly one, but iterate: two logins racing on the same browser can leave a second one
            // behind, and this used to be a SingleOrDefault that turned that into a 500 on every later login.
            List<KeyValuePair<string, RefreshTokenDTO>> liveBrowserTokens = (await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString()))
                .Where(x => x.Value.BrowserId == browserId && x.Value.SupersededByTokenString == null)
                .ToList();

            int graceSeconds = _authPolicySettings.RefreshTokenGraceSeconds;

            foreach (KeyValuePair<string, RefreshTokenDTO> liveBrowserToken in liveBrowserTokens)
            {
                if (graceSeconds <= 0)
                {
                    await _usersRefreshTokens.TryRemoveAsync(liveBrowserToken.Key);
                    continue;
                }

                liveBrowserToken.Value.SupersededByTokenString = successorTokenString;
                // Shortening ExpiresAt is what ends the grace window: the storage TTL follows it, so the
                // token cleans itself up and needs no separate sweep.
                liveBrowserToken.Value.ExpiresAt = DateTime.UtcNow.AddSeconds(graceSeconds);
                await _usersRefreshTokens.AddOrUpdateAsync(liveBrowserToken.Key, liveBrowserToken.Value);
            }
        }

        /// <summary>
        /// If we found the user => true
        /// If we didn't find the user => false
        /// </summary>
        public virtual async Task<bool> RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string? browserId, long userId)
        {
            // Every token for this (user, browser) goes, superseded ones included: logging out has to end the
            // session, not just its newest hop, and a grace window routinely leaves a predecessor behind.
            List<KeyValuePair<string, RefreshTokenDTO>> browserTokens = (await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString()))
                .Where(x => x.Value.BrowserId == browserId)
                .ToList();

            if (browserTokens.Count == 0)
            {
                // TODO: Log
                return false;
            }

            foreach (KeyValuePair<string, RefreshTokenDTO> browserToken in browserTokens)
            {
                await _usersRefreshTokens.TryRemoveAsync(browserToken.Key);
            }

            return true;
        }

        public virtual async Task RemoveExpiredRefreshTokensAsync()
        {
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> expiredTokens = await _usersRefreshTokens.WhereAsync(x => x.Value.ExpiresAt < DateTime.UtcNow);
            foreach (var expiredToken in expiredTokens)
                await _usersRefreshTokens.TryRemoveAsync(expiredToken.Key);
        }

        public virtual async Task RemoveRefreshTokenByUserIdAsync(long userId)
        {
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> refreshTokens = await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString());
            foreach (var refreshToken in refreshTokens)
                await _usersRefreshTokens.TryRemoveAsync(refreshToken.Key);
        }

        private static string GenerateRandomTokenString()
        {
            byte[] randomNumber = new byte[32]; // It would take approximately 1.84 x 10^60 years to guess the token using brute force at a rate of 1 billion guesses per second which is also nearly imposible
            using RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
            randomNumberGenerator.GetBytes(randomNumber);
            return Base64UrlEncoder.Encode(randomNumber); // Making it url safe
        }

        private async Task<bool> IsRefreshTokenWithNewIpAddressAsync(long userId, string? ipAddress)
        {
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> tokens = await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString());
            if (tokens.OrderByDescending(x => x.Value.ExpiresAt).FirstOrDefault().Value?.IpAddress != ipAddress)
                return true;
            else
                return false;
        }

        private async Task RemoveTokensForMoreThenAllowedBrowsersAsync(long userId)
        {
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> tokens = await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString());
            // Superseded tokens are hops of a browser's session, not extra browsers — counting them would let
            // a burst of refreshes look like the user exceeded the cap and evict a session that is still live.
            List<KeyValuePair<string, RefreshTokenDTO>> refreshTokens = tokens.Where(x => x.Value.SupersededByTokenString == null).ToList();
            if (refreshTokens.Count > _authPolicySettings.AllowedBrowsersForTheSingleUser)
            {
                List<KeyValuePair<string, RefreshTokenDTO>> excessBrowserRefreshTokens = refreshTokens.OrderBy(x => x.Value.ExpiresAt).Take(refreshTokens.Count - _authPolicySettings.AllowedBrowsersForTheSingleUser).ToList();
                foreach (KeyValuePair<string, RefreshTokenDTO> refreshToken in excessBrowserRefreshTokens)
                {
                    await _usersRefreshTokens.TryRemoveAsync(refreshToken.Key);
                }
            }
        }

        #endregion

        #endregion

        #region Verification

        #region Login

        public virtual async Task<LoginVerificationTokenDTO> ValidateAndGetLoginVerificationTokenDTOAsync(string verificationTokenKey, string? browserId, string email)
        {
            await RemoveExpiredLoginVerificationTokensAsync();

            // Doing this because there is a chance of generating two same codes.
            LoginVerificationTokenDTO? loginVerificationTokenDTO = await _usersLoginVerificationTokens.TryGetValueAsync(verificationTokenKey);
            if (loginVerificationTokenDTO != null && (loginVerificationTokenDTO.Email != email || loginVerificationTokenDTO.BrowserId != browserId))
                loginVerificationTokenDTO = null;

            if (loginVerificationTokenDTO == null)
                throw new ExpiredVerificationException(_localizer["ExpiredVerificationCodeException"]); // We can not allow user to "send again" from here, because it is deleted

            IEnumerable<KeyValuePair<string, LoginVerificationTokenDTO>> emailTokens = await _usersLoginVerificationTokens.GetByIndexAsync(LoginVerificationTokenDTO.EmailIndex, loginVerificationTokenDTO.Email);
            KeyValuePair<string, LoginVerificationTokenDTO> lastVerificationToken = emailTokens
                .OrderByDescending(x => x.Value.ExpiresAt)
                .FirstOrDefault();

            // TODO: Append additional info in the Log
            if (verificationTokenKey != lastVerificationToken.Key)
                throw new ExpiredVerificationException(_localizer["LatestVerificationCodeException"]);

            return loginVerificationTokenDTO;
        }

        public virtual async Task<string> GenerateAndSaveLoginVerificationCodeAsync(string userEmail, string? browserId)
        {
            DateTime now = DateTime.UtcNow;
            LoginVerificationTokenDTO loginVerificationTokenDTO = new LoginVerificationTokenDTO
            {
                Email = userEmail,
                BrowserId = browserId,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(_authPolicySettings.VerificationTokenExpiration),
            };

            string code = GenerateVerificationCodeKey();
            await _usersLoginVerificationTokens.AddOrUpdateAsync(code, loginVerificationTokenDTO);
            return code;
        }

        /// <summary>
        /// Returns <c>true</c> when a new login-verification email to <paramref name="email"/> should be
        /// suppressed by the per-address resend policy — either because the most recent code was issued
        /// within <see cref="AuthPolicyOptions.VerificationCodeResendCooldownSeconds"/>, or because the
        /// number of currently-valid codes has reached
        /// <see cref="AuthPolicyOptions.MaxActiveVerificationCodesPerEmail"/>. Both limits are per-address
        /// and IP-independent, so they hold against a distributed sender that the per-IP rate limiter
        /// cannot stop. Expired codes are ignored. Callers should silently report success when this returns
        /// <c>true</c> (the caller already holds a fresh code), so the endpoint leaks neither whether an
        /// address exists nor that it is being targeted.
        /// </summary>
        /// <example>
        /// <code>
        /// if (await _jwtAuthManagerService.IsLoginVerificationSendBlockedAsync(userEmail))
        ///     return new SendLoginVerificationEmailResultDTO { Message = string.Empty };
        /// </code>
        /// </example>
        public virtual async Task<bool> IsLoginVerificationSendBlockedAsync(string email)
        {
            DateTime now = DateTime.UtcNow;

            List<LoginVerificationTokenDTO> activeTokens =
                (await _usersLoginVerificationTokens.GetByIndexAsync(LoginVerificationTokenDTO.EmailIndex, email))
                .Select(x => x.Value)
                .Where(x => x.ExpiresAt > now)
                .ToList();

            if (activeTokens.Count == 0)
                return false;

            int cooldownSeconds = _authPolicySettings.VerificationCodeResendCooldownSeconds;
            if (cooldownSeconds > 0 && activeTokens.Max(x => x.CreatedAt) > now.AddSeconds(-cooldownSeconds))
                return true;

            int maxActive = _authPolicySettings.MaxActiveVerificationCodesPerEmail;
            if (maxActive > 0 && activeTokens.Count >= maxActive)
                return true;

            return false;
        }

        #endregion

        #region Helpers

        private static string GenerateVerificationCodeKey()
        {
            int code = Random.Next(100000, 1000000);
            return code.ToString("D6");
        }

        public virtual async Task RemoveLoginVerificationTokensByEmailAsync(string email)
        {
            IEnumerable<KeyValuePair<string, LoginVerificationTokenDTO>> verificationTokens = await _usersLoginVerificationTokens.GetByIndexAsync(LoginVerificationTokenDTO.EmailIndex, email);
            foreach (var verificationToken in verificationTokens)
            {
                await _usersLoginVerificationTokens.TryRemoveAsync(verificationToken.Key);
            }
        }

        private async Task RemoveExpiredLoginVerificationTokensAsync()
        {
            IEnumerable<KeyValuePair<string, LoginVerificationTokenDTO>> expiredTokens = await _usersLoginVerificationTokens.WhereAsync(x => x.Value.ExpiresAt < DateTime.UtcNow);
            foreach (var expiredToken in expiredTokens)
            {
                await _usersLoginVerificationTokens.TryRemoveAsync(expiredToken.Key);
            }
        }

        #endregion

        #endregion

        public void Dispose()
        {
            _generateAccessAndRefreshTokensLock?.Dispose();
        }
    }
}
