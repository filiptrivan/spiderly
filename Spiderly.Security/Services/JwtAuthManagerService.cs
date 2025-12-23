using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Resources;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
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
    public class JwtAuthManagerService : IJwtAuthManager
    {
        private readonly ITokenCacheService _tokenCache;

        public IImmutableDictionary<string, RefreshTokenDTO> UsersRefreshTokensReadOnlyDictionary => throw new NotSupportedException("Use async methods with ITokenCacheService instead");

        public IImmutableDictionary<string, LoginVerificationTokenDTO> UsersLoginVerificationTokensReadOnlyDictionary => throw new NotSupportedException("Use async methods with ITokenCacheService instead");

        private static readonly Random Random = new();

        public JwtAuthManagerService(ITokenCacheService tokenCache)
        {
            _tokenCache = tokenCache;
        }

        #region Refresh

        /// <summary>
        /// 1. Stole refresh but doesn't have access - we validate if he has access
        /// 2. Stole a refresh from one user, he has his own valid access - we log both of them out because they have different emails
        /// 3. Stole access but no refresh
        /// 4. Stole both - we can't do anything to him, we only try to stop him if he's on a different ip address
        /// </summary>
        public async Task<JwtAuthResultDTO> Refresh(RefreshTokenRequestDTO request, long userIdFromAccessToken)
        {
            await RemoveTokensForMoreThenAllowedBrowsers(userIdFromAccessToken);

            RefreshTokenDTO? existingRefreshToken = await _tokenCache.GetRefreshTokenAsync(userIdFromAccessToken.ToString(), request.BrowserId);

            if (existingRefreshToken == null)
            {
                throw new SecurityTokenException(SharedTerms.ExpiredRefreshTokenException);
            }

            if (existingRefreshToken.UserId != userIdFromAccessToken)
            {
                await RemoveRefreshTokenByUserId(existingRefreshToken.UserId);
                await RemoveRefreshTokenByUserId(userIdFromAccessToken);
                throw new HackerException("The user id can't be different in refresh and access token.");
            }
            if (SettingsProvider.Current.AllowTheUseOfAppWithDifferentIpAddresses == false && await IsRefreshTokenWithNewIpAddress(existingRefreshToken.UserId, existingRefreshToken.IpAddress) == true)
            {
                await RemoveRefreshTokenByUserId(existingRefreshToken.UserId);
                throw new SecurityTokenException(SharedTerms.TwoDifferentIpAddressesRefreshException);
            }

            return await GenerateAccessAndRefreshTokens(userIdFromAccessToken, existingRefreshToken.IpAddress, request.BrowserId);
        }

        private readonly object _generateAccessAndRefreshTokensLock = new();

        /// <summary>
        /// Password and verificationExpiration (minutes) are only needed if we are registering the account, for email verification
        /// </summary>
        public JwtAuthResultDTO GenerateAccessAndRefreshTokens(long userId, string ipAddress, string browserId)
        {
            List<Claim> claims = GenerateClaims(userId);

            string accessToken = GenerateAccessToken(claims);

            RefreshTokenDTO refreshTokenDTO = new RefreshTokenDTO
            {
                UserId = userId,
                IpAddress = ipAddress,
                BrowserId = browserId,
                TokenString = GenerateRandomTokenString(),
                ExpireAt = DateTime.UtcNow.AddMinutes(SettingsProvider.Current.RefreshTokenExpiration),
            };

            lock (_generateAccessAndRefreshTokensLock)
            {
                RemoveLastRefreshTokenFromTheSameBrowserAndUserId(browserId, userId); // FT: userId also because the hacker could manipulate browserId, but he can't userId

                // It will always generate new token,
                // it is beneficial if the user open the application from different devices
                // if the user open the application on the multiple tabs in the same browser, we are working with the local storage so it will not make the difference
                _usersRefreshTokens.AddOrUpdate(refreshTokenDTO.TokenString, refreshTokenDTO, (_, _) => refreshTokenDTO);
            }

            return new JwtAuthResultDTO
            {
                UserId = userId,
                AccessToken = accessToken,
                Token = refreshTokenDTO
            };
        }

        public List<Claim> GenerateClaims(long userId)
        {
            return new List<Claim>
            {
                new Claim(ClaimTypes.PrimarySid, userId.ToString()),
            };
        }

        #region Helpers

        private string GenerateAccessToken(List<Claim> claims, int? verificationExpiration = null)
        {
            byte[] secretKey = Encoding.UTF8.GetBytes(SettingsProvider.Current.JwtKey);
            SigningCredentials credentials = new SigningCredentials(new SymmetricSecurityKey(secretKey), SecurityAlgorithms.HmacSha256Signature);

            bool shouldAddAudienceClaim = string.IsNullOrWhiteSpace(claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Aud)?.Value);
            JwtSecurityToken jwtToken = new JwtSecurityToken(
                SettingsProvider.Current.JwtIssuer,
                shouldAddAudienceClaim ? SettingsProvider.Current.JwtAudience : string.Empty,
                claims,
                expires: DateTime.UtcNow.AddMinutes(verificationExpiration ?? SettingsProvider.Current.AccessTokenExpiration),
                signingCredentials: credentials);

            string accessToken = new JwtSecurityTokenHandler().WriteToken(jwtToken);

            return accessToken;
        }

        public List<Claim> GetClaimsForTheAccessToken(RefreshTokenRequestDTO request, string accessToken)
        {
            List<Claim> principalClaims;

            try
            {
                JwtSecurityToken jwtToken = ValidateJwtToken(accessToken); // FT: We are not validating the jwt token here, we are just reading claims so we don't need to go to the database
                principalClaims = jwtToken.Claims.ToList();
            }
            catch (Exception)
            {
                _usersRefreshTokens.TryRemove(request.RefreshToken, out _); // FT: If the user hadn't access token but trying somehow to do something ilegal, remove the passed refresh also
                throw;
            }

            return principalClaims; // FT: Its not possible to return null, if there is no exception it will return, if there is the catch block will throw
        }

        private JwtSecurityToken ValidateJwtToken(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new SecurityTokenException(SharedTerms.ExpiredRefreshTokenException); // FT: It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            byte[] secretKey = Encoding.UTF8.GetBytes(SettingsProvider.Current.JwtKey);

            new JwtSecurityTokenHandler()
                .ValidateToken(accessToken,
                    new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = SettingsProvider.Current.JwtIssuer,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(secretKey),
                        ValidAudience = SettingsProvider.Current.JwtAudience,
                        ValidateAudience = true, // Checking if the audience is the valid one (localhost:7260)
                        ValidateLifetime = false, // If the token has expired, it will not be valid, so we don't need to do something like this: if (existingRefreshToken.ExpireAt - jwtToken.ExpireAt > SettingsProvider.Current.RefreshTokenExpiration - SettingsProvider.Current.AccessTokenExpiration) ...
                        ClockSkew = TimeSpan.FromMinutes(SettingsProvider.Current.ClockSkewMinutes)
                    },
            out SecurityToken validatedToken);

            JwtSecurityToken jwtToken = validatedToken as JwtSecurityToken;

            if (jwtToken == null || !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256Signature)) // Validating JWT token, checking if it has changed claims etc.
                throw new HackerException("Hacker is trying to change the jwt token.");

            return jwtToken;
        }

        /// <summary>
        /// FT: If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user
        /// </summary>
        public void Logout(string browserId, long userId)
        {
            bool foundTheUser = RemoveLastRefreshTokenFromTheSameBrowserAndUserId(browserId, userId);

            if (foundTheUser == false)
            {
                RemoveRefreshTokenByUserId(userId);
            }
        }

        /// <summary>
        /// If we found the user => true
        /// If we didn't find the user => false
        /// </summary>
        public bool RemoveLastRefreshTokenFromTheSameBrowserAndUserId(string browserId, long userId)
        {
            // TODO FT: Log if the email or browser id is null

            // FT: ToList() because it somehow happened that the same user clicks fast two times and send two requests with 
            KeyValuePair<string, RefreshTokenDTO> refreshToken = _usersRefreshTokens.Where(x => x.Value.BrowserId == browserId && x.Value.UserId == userId).SingleOrDefault();

            if (string.IsNullOrEmpty(refreshToken.Key))
            {
                // TODO FT: Log
                return false;
            }
            else
            {
                _usersRefreshTokens.TryRemove(refreshToken.Key, out _);

                return true;
            }
        }

        public void RemoveExpiredRefreshTokens()
        {
            var expiredTokens = _usersRefreshTokens.Where(x => x.Value.ExpireAt < DateTime.UtcNow).ToList();
            foreach (var expiredToken in expiredTokens)
                _usersRefreshTokens.TryRemove(expiredToken.Key, out _);
        }

        public void RemoveRefreshTokenByUserId(long userId)
        {
            var refreshTokens = _usersRefreshTokens.Where(x => x.Value.UserId == userId).ToList();
            foreach (var refreshToken in refreshTokens)
                _usersRefreshTokens.TryRemove(refreshToken.Key, out _);
        }

        private static string GenerateRandomTokenString()
        {
            byte[] randomNumber = new byte[32]; // It would take approximately 1.84 x 10^60 years to guess the token using brute force at a rate of 1 billion guesses per second which is also nearly imposible
            using RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create();
            randomNumberGenerator.GetBytes(randomNumber);
            return Base64UrlEncoder.Encode(randomNumber); // FT: Making it url safe
        }

        private bool IsRefreshTokenWithNewIpAddress(long userId, string ipAddress)
        {
            if (_usersRefreshTokens.Where(x => x.Value.UserId == userId).OrderByDescending(x => x.Value.ExpireAt).FirstOrDefault().Value?.IpAddress != ipAddress)
                return true;
            else
                return false;
        }

        private void RemoveTokensForMoreThenAllowedBrowsers(long userId)
        {
            List<KeyValuePair<string, RefreshTokenDTO>> refreshTokens = _usersRefreshTokens.Where(x => x.Value.UserId == userId).ToList();
            if (refreshTokens.Count > SettingsProvider.Current.AllowedBrowsersForTheSingleUser)
            {
                List<KeyValuePair<string, RefreshTokenDTO>> excessBrowserRefreshTokens = refreshTokens.OrderBy(x => x.Value.ExpireAt).Take(refreshTokens.Count - SettingsProvider.Current.AllowedBrowsersForTheSingleUser).ToList();
                foreach (KeyValuePair<string, RefreshTokenDTO> refreshToken in excessBrowserRefreshTokens)
                {
                    _usersRefreshTokens.TryRemove(refreshToken.Key, out _);
                }
            }
        }

        #endregion

        #endregion

        #region Verification

        #region Login

        public LoginVerificationTokenDTO ValidateAndGetLoginVerificationTokenDTO(string verificationTokenKey, string browserId, string email)
        {
            RemoveExpiredLoginVerificationTokens();

            // FT: Doing this because there is a chance of generating two same codes.
            LoginVerificationTokenDTO loginVerificationTokenDTO = _usersLoginVerificationTokens.Where(x => x.Key == verificationTokenKey && x.Value.Email == email && x.Value.BrowserId == browserId).SingleOrDefault().Value;

            if (loginVerificationTokenDTO == null)
                throw new ExpiredVerificationException(); // FT: We can not allow user to "send again" from here, because it is deleted

            KeyValuePair<string, LoginVerificationTokenDTO> lastVerificationToken = _usersLoginVerificationTokens
                .Where(x => x.Value.Email == loginVerificationTokenDTO.Email)
                .OrderByDescending(x => x.Value.ExpireAt)
                .FirstOrDefault();

            // TODO FT: Append additional info in the Log 
            if (verificationTokenKey != lastVerificationToken.Key)
                throw new ExpiredVerificationException(SharedTerms.LatestVerificationCodeException);

            return loginVerificationTokenDTO;
        }

        public string GenerateAndSaveLoginVerificationCode(string userEmail, long userId, string browserId)
        {
            LoginVerificationTokenDTO loginVerificationTokenDTO = new LoginVerificationTokenDTO
            {
                Email = userEmail,
                BrowserId = browserId,
                ExpireAt = DateTime.UtcNow.AddMinutes(SettingsProvider.Current.VerificationTokenExpiration),
            };

            string code = GenerateVerificationCodeKey();
            _usersLoginVerificationTokens.AddOrUpdate(code, loginVerificationTokenDTO, (_, _) => loginVerificationTokenDTO);
            return code;
        }

        #endregion

        #region Helpers

        private static string GenerateVerificationCodeKey()
        {
            int code = Random.Next(100000, 1000000);
            return code.ToString("D6");
        }

        public void RemoveLoginVerificationTokensByEmail(string email)
        {
            var verificationTokens = _usersLoginVerificationTokens.Where(x => x.Value.Email == email).ToList();
            foreach (var verificationToken in verificationTokens)
            {
                _usersLoginVerificationTokens.TryRemove(verificationToken.Key, out _);
            }
        }

        private void RemoveExpiredLoginVerificationTokens()
        {
            var expiredTokens = _usersLoginVerificationTokens.Where(x => x.Value.ExpireAt < DateTime.UtcNow).ToList();
            foreach (var expiredToken in expiredTokens)
            {
                _usersLoginVerificationTokens.TryRemove(expiredToken.Key, out _);
            }
        }

        #endregion

        #endregion

    }
}
