using Microsoft.Extensions.Localization;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
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
        private readonly IJwtSettings _jwtSettings;
        private readonly IAuthPolicySettings _authPolicySettings;

        private static readonly Random Random = new();
        private static readonly JsonWebTokenHandler _jsonWebTokenHandler = new();

        public JwtAuthManagerService(
            ITokenStorage<RefreshTokenDTO> refreshTokenStorage,
            ITokenStorage<LoginVerificationTokenDTO> loginVerificationTokenStorage,
            IStringLocalizer localizer,
            IJwtSettings jwtSettings,
            IAuthPolicySettings authPolicySettings)
        {
            _usersRefreshTokens = refreshTokenStorage;
            _usersLoginVerificationTokens = loginVerificationTokenStorage;
            _localizer = localizer;
            _jwtSettings = jwtSettings;
            _authPolicySettings = authPolicySettings;
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

            // Sometimes in development mode, when the multiple tabs are open, on the save of the angular app we refresh the tabs in the same time, so we don't even manage to change the value of the refresh token in the local storage,
            // and we send another request with the same refresh token as the previous one, and since we deleted it, it doesn't exist
            RefreshTokenDTO existingRefreshToken = await _usersRefreshTokens.TryGetValueAsync(request.RefreshToken);
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
            if (_authPolicySettings.AllowTheUseOfAppWithDifferentIpAddresses == false && await IsRefreshTokenWithNewIpAddressAsync(existingRefreshToken.UserId, existingRefreshToken.IpAddress) == true)
            {
                // cuvas device-ove koje je cesto korisio, guras ih u familiju uredjaja, po nekom algoritmu odredi neki koji ti se cini sumnjiv i
                // na njemu mu trazi multifaktor aut. ako je klijent uopste trazio multifaktor
                await RemoveRefreshTokenByUserIdAsync(existingRefreshToken.UserId); // Don't need to delete for userDTO also, because we already did that
                throw new SecurityTokenException(_localizer["TwoDifferentIpAddressesRefreshException"]);
            }

            return await GenerateAccessAndRefreshTokensAsync(userId, existingRefreshToken.IpAddress, request.BrowserId); // need to recover the original claims
        }

        protected readonly SemaphoreSlim _generateAccessAndRefreshTokensLock = new(1, 1);

        /// <summary>
        /// Password and verificationExpiration (minutes) are only needed if we are registering the account, for email verification
        /// </summary>
        public virtual async Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokensAsync(long userId, string ipAddress, string browserId)
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
                await RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(browserId, userId); // userId also because the hacker could manipulate browserId, but he can't userId

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
        /// </summary>
        public virtual List<Claim> GenerateClaims(long userId)
        {
            return new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            };
        }

        #region Helpers

        private AccessTokenDTO GenerateAccessToken(List<Claim> claims, int? verificationExpiration = null)
        {
            byte[] secretKey = Encoding.UTF8.GetBytes(_jwtSettings.JwtKey);
            SigningCredentials credentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256);
            DateTime expiresAt = DateTime.UtcNow.AddMinutes(verificationExpiration ?? _authPolicySettings.AccessTokenExpiration);

            SecurityTokenDescriptor tokenDescriptor = new SecurityTokenDescriptor
            {
                Issuer = _jwtSettings.JwtIssuer,
                Audience = _jwtSettings.JwtAudience,
                Expires = expiresAt,
                Subject = new ClaimsIdentity(claims),
                SigningCredentials = credentials,
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
                await _usersRefreshTokens.TryRemoveAsync(request.RefreshToken); // If the user hadn't access token but trying somehow to do something ilegal, remove the passed refresh also
                throw;
            }

            return principalClaims; // It's not possible to return null, if there is no exception it will return, if there is the catch block will throw
        }

        private async Task<JsonWebToken> ValidateJwtTokenAsync(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]); // It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            byte[] secretKey = Encoding.UTF8.GetBytes(_jwtSettings.JwtKey);

            TokenValidationResult result = await _jsonWebTokenHandler
                .ValidateTokenAsync(accessToken,
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = _jwtSettings.JwtIssuer,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                        ValidAudience = _jwtSettings.JwtAudience,
                        ValidateAudience = true, // Checking if the audience is the valid one (localhost:7260)
                        ValidateLifetime = false, // If the token has expired, it will not be valid, so we don't need to do something like this: if (existingRefreshToken.ExpiresAt - jwtToken.ExpiresAt > RefreshTokenExpiration - AccessTokenExpiration) ...
                        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }, // Pin HS256 to block alg-downgrade (e.g. "none")
                        ClockSkew = TimeSpan.FromMinutes(_jwtSettings.ClockSkewMinutes)
                    });

            if (result.IsValid == false) // ValidateTokenAsync reports failure via IsValid instead of throwing; the flow expects an exception
                throw new SecurityTokenException(_localizer["ExpiredRefreshTokenException"]);

            if (result.SecurityToken is not JsonWebToken jwtToken)
                throw new InvalidOperationException("Expected a JsonWebToken from a successful validation result.");

            return jwtToken;
        }

        /// <summary>
        /// If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user
        /// </summary>
        public virtual async Task LogoutAsync(string browserId, long userId)
        {
            bool foundTheUser = await RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(browserId, userId);

            if (foundTheUser == false)
            {
                await RemoveRefreshTokenByUserIdAsync(userId);
            }
        }

        /// <summary>
        /// If we found the user => true
        /// If we didn't find the user => false
        /// </summary>
        public virtual async Task<bool> RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string browserId, long userId)
        {
            // TODO Log if the email or browser id is null

            // ToList() because it somehow happened that the same user clicks fast two times and send two requests with
            IEnumerable<KeyValuePair<string, RefreshTokenDTO>> tokens = (await _usersRefreshTokens.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString()))
                .Where(x => x.Value.BrowserId == browserId)
                .ToList();
            KeyValuePair<string, RefreshTokenDTO> refreshToken = tokens.SingleOrDefault();

            if (string.IsNullOrEmpty(refreshToken.Key))
            {
                // TODO: Log
                return false;
            }
            else
            {
                await _usersRefreshTokens.TryRemoveAsync(refreshToken.Key);

                return true;
            }
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

        private async Task<bool> IsRefreshTokenWithNewIpAddressAsync(long userId, string ipAddress)
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
            List<KeyValuePair<string, RefreshTokenDTO>> refreshTokens = tokens.ToList();
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

        public virtual async Task<LoginVerificationTokenDTO> ValidateAndGetLoginVerificationTokenDTOAsync(string verificationTokenKey, string browserId, string email)
        {
            await RemoveExpiredLoginVerificationTokensAsync();

            // Doing this because there is a chance of generating two same codes.
            LoginVerificationTokenDTO loginVerificationTokenDTO = await _usersLoginVerificationTokens.TryGetValueAsync(verificationTokenKey);
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

        public virtual async Task<string> GenerateAndSaveLoginVerificationCodeAsync(string userEmail, string browserId)
        {
            LoginVerificationTokenDTO loginVerificationTokenDTO = new LoginVerificationTokenDTO
            {
                Email = userEmail,
                BrowserId = browserId,
                ExpiresAt = DateTime.UtcNow.AddMinutes(_authPolicySettings.VerificationTokenExpiration),
            };

            string code = GenerateVerificationCodeKey();
            await _usersLoginVerificationTokens.AddOrUpdateAsync(code, loginVerificationTokenDTO);
            return code;
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
