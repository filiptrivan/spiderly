using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Services;
using Spiderly.Shared;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Every refresh rotates the refresh token, so two refreshes that the browser composed before either
    /// response's <c>Set-Cookie</c> came back both carry the same token string. Without a grace window the
    /// second one finds a token that the first has already replaced and fails — and because the 401 handler
    /// clears the auth cookies, the loser of that race destroys the session the winner just established.
    /// Concurrent refreshes are normal here (a second tab, or another app under the same cookie domain), so
    /// these tests pin that the superseded token keeps resolving to its successor for a short window.
    /// </summary>
    public class RefreshTokenRotationGraceTests
    {
        private const string ValidKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const long UserId = 42;
        private const string BrowserId = "browser-1";
        private const string IpAddress = "203.0.113.7";

        [Fact]
        public async Task Refresh_WithASupersededToken_ReturnsTheSuccessorInsteadOfThrowing()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;

            // First tab wins the race and rotates the token.
            string successor = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;

            // Second tab's request was already in flight with the original token string.
            JwtAuthResultDTO replay = await RefreshAsync(sut, original);

            Assert.Equal(successor, replay.RefreshTokenDTO.TokenString);
        }

        [Fact]
        public async Task Refresh_WithASupersededToken_IssuesAWorkingAccessToken()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            await RefreshAsync(sut, original);

            JwtAuthResultDTO replay = await RefreshAsync(sut, original);

            // Replaying must not hand back a stale access token — the caller refreshed precisely because
            // its own was expiring.
            Assert.False(string.IsNullOrWhiteSpace(replay.AccessTokenDTO.TokenString));
            Assert.True(replay.AccessTokenDTO.ExpiresAt > DateTime.UtcNow);
            Assert.Equal(UserId, replay.UserId);
        }

        [Fact]
        public async Task Refresh_WithASupersededToken_DoesNotRotateAgain()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            string successor = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;

            await RefreshAsync(sut, original);

            // A replay that rotated would leave the winning tab holding a token that is itself superseded,
            // so the very next refresh would race again and the cascade would never settle.
            JwtAuthResultDTO next = await RefreshAsync(sut, successor);
            Assert.NotEqual(successor, next.RefreshTokenDTO.TokenString);
        }

        [Fact]
        public async Task Refresh_ResolvesThroughAChainOfSupersededTokens()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            string second = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;
            string third = (await RefreshAsync(sut, second)).RefreshTokenDTO.TokenString;

            // A tab that slept through two rotations still holds the original.
            JwtAuthResultDTO replay = await RefreshAsync(sut, original);

            Assert.Equal(third, replay.RefreshTokenDTO.TokenString);
        }

        [Fact]
        public async Task Refresh_WithASupersededToken_PastTheGraceWindow_Throws()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out InMemoryTokenStorage<RefreshTokenDTO> store);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            await RefreshAsync(sut, original);
            await ExpireAsync(store, original);

            // Past the window it is an ordinary dead token again: a genuinely stale client must be sent to
            // the login page, not handed a live session.
            await Assert.ThrowsAsync<SecurityTokenException>(() => RefreshAsync(sut, original));
        }

        [Fact]
        public async Task Refresh_WithAnUnknownToken_Throws()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            await LoginAsync(sut);

            await Assert.ThrowsAsync<SecurityTokenException>(() => RefreshAsync(sut, "never-issued"));
        }

        [Fact]
        public async Task Login_WhileASupersededTokenIsStillStored_DoesNotThrow()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            await RefreshAsync(sut, original);

            // The grace window leaves two tokens for one (user, browser) pair. Anything that looks up "the"
            // token for a browser has to tolerate that instead of blowing up on the second one.
            JwtAuthResultDTO relogin = await LoginAsync(sut);

            Assert.False(string.IsNullOrWhiteSpace(relogin.RefreshTokenDTO.TokenString));
        }

        [Fact]
        public async Task GraceDisabled_DropsThePreviousTokenImmediately()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions { RefreshTokenGraceSeconds = 0 }, out _);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            await RefreshAsync(sut, original);

            await Assert.ThrowsAsync<SecurityTokenException>(() => RefreshAsync(sut, original));
        }

        #region Harness

        private static Task<JwtAuthResultDTO> LoginAsync(JwtAuthManagerService sut)
            => sut.GenerateAccessAndRefreshTokensAsync(UserId, IpAddress, BrowserId);

        private static Task<JwtAuthResultDTO> RefreshAsync(JwtAuthManagerService sut, string refreshToken)
            => sut.RefreshAsync(new RefreshTokenRequestDTO { RefreshToken = refreshToken, BrowserId = BrowserId }, UserId);

        /// <summary>Ages a stored token out of its grace window without waiting for the clock.</summary>
        private static async Task ExpireAsync(InMemoryTokenStorage<RefreshTokenDTO> store, string tokenString)
        {
            RefreshTokenDTO token = (await store.TryGetValueAsync(tokenString))!;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await store.AddOrUpdateAsync(tokenString, token);
        }

        /// <summary>Builds a manager over a fresh in-memory refresh store wired with the same user index as production.</summary>
        private static JwtAuthManagerService BuildSut(AuthPolicyOptions policy, out InMemoryTokenStorage<RefreshTokenDTO> refreshStore)
        {
            JwtOptions jwtOptions = new() { JwtKey = ValidKey, JwtIssuer = "https://test-issuer", JwtAudience = "https://test-audience", ClockSkewMinutes = 0 };

            refreshStore = new InMemoryTokenStorage<RefreshTokenDTO>(
                new Dictionary<string, Func<RefreshTokenDTO, string?>>
                {
                    { RefreshTokenDTO.UserIdIndex, t => t.UserId.ToString() },
                });

            return new JwtAuthManagerService(
                refreshStore,
                new InMemoryTokenStorage<LoginVerificationTokenDTO>(),
                new PassthroughStringLocalizer(),
                Options.Create(jwtOptions),
                Options.Create(policy));
        }

        #endregion
    }
}
