using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Services;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Localization;
using SharedSettings = Spiderly.Shared.Settings;
using SecuritySettings = Spiderly.Security.Settings;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Codec-layer tests for <see cref="JwtAuthManagerService"/> — token issuance
    /// (<c>GenerateAccessToken</c>) and validation (<c>ValidateJwtTokenAsync</c>) after the
    /// migration from <c>JwtSecurityTokenHandler</c> to <c>JsonWebTokenHandler</c>.
    /// Issuance is exercised through the public <c>GenerateAccessAndRefreshTokensAsync</c> backed
    /// by a real in-memory store; validation through the public <c>GetClaimsForTheAccessTokenAsync</c>.
    /// Refresh orchestration (IP / multi-browser / rotation) is intentionally not covered here.
    /// </summary>
    public class JwtAuthManagerServiceTests
    {
        // 64 ASCII bytes — valid key size for both HS256 (the service) and HS512 (the alg-pin test).
        private const string ValidKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string Issuer = "https://test-issuer";
        private const string Audience = "https://test-audience";
        private const long UserId = 42;

        private readonly JwtAuthManagerService _sut;

        public JwtAuthManagerServiceTests()
        {
            // JWT config is injected, so each test instance gets its own settings — no global static to
            // mutate and no teardown. Tests are fully isolated and safe to parallelize. SharedSettings /
            // SecuritySettings implement the injected IJwtSettings / IAuthPolicySettings views; their
            // defaults are fine for the codec, we only pin the JWT signing/issuer/audience values.
            SharedSettings jwtSettings = new()
            {
                JwtKey = ValidKey,
                JwtIssuer = Issuer,
                JwtAudience = Audience,
                ClockSkewMinutes = 0,
            };

            InMemoryTokenStorage<RefreshTokenDTO> refreshStore = new(
                new Dictionary<string, Func<RefreshTokenDTO, string>>
                {
                    { RefreshTokenDTO.UserIdIndex, t => t.UserId.ToString() },
                });
            InMemoryTokenStorage<LoginVerificationTokenDTO> verificationStore = new();

            _sut = new JwtAuthManagerService(refreshStore, verificationStore, new PassthroughStringLocalizer(), jwtSettings, new SecuritySettings());
        }

        [Fact]
        public async Task RoundTrip_ValidatesAndYieldsSubClaim()
        {
            string token = await IssueAccessTokenAsync();

            List<Claim> claims = await ValidateAsync(token);

            Assert.Equal(UserId.ToString(), claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        }

        [Fact]
        public async Task IssuedToken_UsesHs256Algorithm()
        {
            // Locks the one behavioral unknown of the migration: what alg JsonWebTokenHandler.CreateToken emits.
            string token = await IssueAccessTokenAsync();

            JsonWebToken parsed = new(token);

            Assert.Equal(SecurityAlgorithms.HmacSha256, parsed.Alg);
        }

        [Fact]
        public async Task TamperedToken_ThrowsSecurityTokenException()
        {
            string token = await IssueAccessTokenAsync();
            string tampered = token[..^3] + (token.EndsWith("aaa") ? "bbb" : "aaa");

            await Assert.ThrowsAsync<SecurityTokenException>(() => ValidateAsync(tampered));
        }

        [Fact]
        public async Task WrongSigningKey_ThrowsSecurityTokenException()
        {
            string token = BuildToken("ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff", Issuer, Audience, SecurityAlgorithms.HmacSha256);

            await Assert.ThrowsAsync<SecurityTokenException>(() => ValidateAsync(token));
        }

        [Fact]
        public async Task WrongIssuer_ThrowsSecurityTokenException()
        {
            string token = BuildToken(ValidKey, "https://evil-issuer", Audience, SecurityAlgorithms.HmacSha256);

            await Assert.ThrowsAsync<SecurityTokenException>(() => ValidateAsync(token));
        }

        [Fact]
        public async Task WrongAudience_ThrowsSecurityTokenException()
        {
            string token = BuildToken(ValidKey, Issuer, "https://evil-audience", SecurityAlgorithms.HmacSha256);

            await Assert.ThrowsAsync<SecurityTokenException>(() => ValidateAsync(token));
        }

        [Fact]
        public async Task ExpiredToken_StillParses_BecauseLifetimeValidationIsOff()
        {
            string token = BuildToken(ValidKey, Issuer, Audience, SecurityAlgorithms.HmacSha256, DateTime.UtcNow.AddHours(-1));

            List<Claim> claims = await ValidateAsync(token);

            Assert.Equal(UserId.ToString(), claims.Single(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        }

        [Fact]
        public async Task NonHs256Algorithm_RejectedByValidAlgorithmsPin()
        {
            // Signed with the correct key but HS512, so the only reason for rejection is the ValidAlgorithms pin.
            string token = BuildToken(ValidKey, Issuer, Audience, SecurityAlgorithms.HmacSha512);

            await Assert.ThrowsAsync<SecurityTokenException>(() => ValidateAsync(token));
        }

        private async Task<string> IssueAccessTokenAsync()
        {
            JwtAuthResultDTO result = await _sut.GenerateAccessAndRefreshTokensAsync(UserId, "127.0.0.1", "browser-1");
            return result.AccessTokenDTO.TokenString;
        }

        private Task<List<Claim>> ValidateAsync(string accessToken) =>
            _sut.GetClaimsForTheAccessTokenAsync(new RefreshTokenRequestDTO { RefreshToken = "dummy" }, accessToken);

        private static string BuildToken(string key, string issuer, string audience, string algorithm, DateTime? expires = null)
        {
            SigningCredentials credentials = new(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), algorithm);
            SecurityTokenDescriptor descriptor = new()
            {
                Issuer = issuer,
                Audience = audience,
                Expires = expires ?? DateTime.UtcNow.AddMinutes(20),
                Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, UserId.ToString()) }),
                SigningCredentials = credentials,
            };
            return new JsonWebTokenHandler().CreateToken(descriptor);
        }
    }
}
