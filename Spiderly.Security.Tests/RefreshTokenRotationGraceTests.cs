using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Services;
using Spiderly.Shared;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Two refreshes a browser composed before either response's <c>Set-Cookie</c> came back carry the same
    /// token string, so the second one meets a token the first has already rotated. That is routine (a second
    /// tab, or another app under the same cookie domain) and it used to end the session rather than the
    /// request, because the 401 handler clears the auth cookies. These tests pin the grace window that keeps
    /// the superseded token resolving to the one that replaced it — see
    /// <see cref="AuthPolicyOptions.RefreshTokenGraceSeconds"/> for the rationale.
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
            JwtAuthManagerService sut = BuildSut();
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
            JwtAuthManagerService sut = BuildSut();
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
            JwtAuthManagerService sut = BuildSut(out InMemoryTokenStorage<RefreshTokenDTO> store);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            string successor = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;

            await RefreshAsync(sut, original);

            // Rotating on a replay would retire the successor too, so the client that won the race would end
            // up holding a superseded token in turn and the two would never settle.
            Assert.Equal(new[] { successor }, await LiveTokenStringsAsync(store));
        }

        [Fact]
        public async Task Refresh_WithATokenFromSeveralRotationsAgo_ReturnsTheLiveToken()
        {
            JwtAuthManagerService sut = BuildSut();
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            string second = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;
            string third = (await RefreshAsync(sut, second)).RefreshTokenDTO.TokenString;

            // A tab that slept through two rotations still holds the original. How far back it is must not
            // matter: resolution is a lookup of the one live token, not a walk back through the rotations.
            JwtAuthResultDTO replay = await RefreshAsync(sut, original);

            Assert.Equal(third, replay.RefreshTokenDTO.TokenString);
        }

        [Fact]
        public async Task Refresh_WithASupersededToken_WhoseSessionIsGone_Throws()
        {
            JwtAuthManagerService sut = BuildSut(out InMemoryTokenStorage<RefreshTokenDTO> store);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            string successor = (await RefreshAsync(sut, original)).RefreshTokenDTO.TokenString;
            await store.TryRemoveAsync(successor);

            // Nothing live left to resolve to: the session ended (logout, revocation) while a predecessor was
            // still inside its window, and a grace record must not outlive the session it belonged to.
            await Assert.ThrowsAsync<SecurityTokenException>(() => RefreshAsync(sut, original));
        }

        [Fact]
        public async Task Refresh_WithASupersededToken_PastTheGraceWindow_Throws()
        {
            JwtAuthManagerService sut = BuildSut(out InMemoryTokenStorage<RefreshTokenDTO> store);
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
            JwtAuthManagerService sut = BuildSut();
            await LoginAsync(sut);

            await Assert.ThrowsAsync<SecurityTokenException>(() => RefreshAsync(sut, "never-issued"));
        }

        [Fact]
        public async Task Login_WhileASupersededTokenIsStillStored_DoesNotThrow()
        {
            JwtAuthManagerService sut = BuildSut(out InMemoryTokenStorage<RefreshTokenDTO> store);
            string original = (await LoginAsync(sut)).RefreshTokenDTO.TokenString;
            await RefreshAsync(sut, original);

            // The grace window leaves two tokens for one (user, browser) pair. Anything that looks up "the"
            // token for a browser has to tolerate that instead of blowing up on the second one.
            JwtAuthResultDTO relogin = await LoginAsync(sut);

            Assert.Equal(new[] { relogin.RefreshTokenDTO.TokenString }, await LiveTokenStringsAsync(store));
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

        private static async Task<List<string>> LiveTokenStringsAsync(InMemoryTokenStorage<RefreshTokenDTO> store)
            => (await store.GetAllAsync()).Select(x => x.Value).Where(x => x.IsSuperseded == false).Select(x => x.TokenString).ToList();

        /// <summary>Ages a stored token out of its grace window without waiting for the clock.</summary>
        private static async Task ExpireAsync(InMemoryTokenStorage<RefreshTokenDTO> store, string tokenString)
        {
            RefreshTokenDTO token = (await store.TryGetValueAsync(tokenString))!;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await store.AddOrUpdateAsync(tokenString, token);
        }

        private static JwtAuthManagerService BuildSut() => BuildSut(new AuthPolicyOptions(), out _);

        private static JwtAuthManagerService BuildSut(out InMemoryTokenStorage<RefreshTokenDTO> refreshStore)
            => BuildSut(new AuthPolicyOptions(), out refreshStore);

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
