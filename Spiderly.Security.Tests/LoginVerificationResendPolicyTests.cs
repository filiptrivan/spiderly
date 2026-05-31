using Microsoft.Extensions.Options;
using Spiderly.Security.DTO;
using Spiderly.Security.Services;
using Spiderly.Shared;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Tests the per-recipient resend policy that guards <c>SendLoginVerificationEmail</c> against
    /// inbox-flooding / email-quota abuse on the (storefront-and-admin-shared) login endpoint:
    /// <see cref="JwtAuthManagerService.IsLoginVerificationSendBlockedAsync"/>. Both limits are
    /// per-address and IP-independent, so they hold against a distributed sender the per-IP rate
    /// limiter cannot stop.
    /// </summary>
    public class LoginVerificationResendPolicyTests
    {
        private const string ValidKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
        private const string Email = "victim@example.com";

        /// <summary>Builds a manager over a fresh in-memory verification store wired with the same email index as production.</summary>
        private static JwtAuthManagerService BuildSut(AuthPolicyOptions policy, out InMemoryTokenStorage<LoginVerificationTokenDTO> verificationStore)
        {
            JwtOptions jwtOptions = new() { JwtKey = ValidKey, JwtIssuer = "https://test-issuer", JwtAudience = "https://test-audience", ClockSkewMinutes = 0 };

            verificationStore = new InMemoryTokenStorage<LoginVerificationTokenDTO>(
                new Dictionary<string, Func<LoginVerificationTokenDTO, string>>
                {
                    { LoginVerificationTokenDTO.EmailIndex, t => t.Email },
                });

            return new JwtAuthManagerService(
                new InMemoryTokenStorage<RefreshTokenDTO>(),
                verificationStore,
                new PassthroughStringLocalizer(),
                Options.Create(jwtOptions),
                Options.Create(policy));
        }

        [Fact]
        public async Task FreshEmail_WithNoOutstandingCodes_IsNotBlocked()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions(), out _);

            Assert.False(await sut.IsLoginVerificationSendBlockedAsync(Email));
        }

        [Fact]
        public async Task SecondRequest_WithinCooldown_IsBlocked()
        {
            JwtAuthManagerService sut = BuildSut(new AuthPolicyOptions { VerificationCodeResendCooldownSeconds = 60 }, out _);
            await sut.GenerateAndSaveLoginVerificationCodeAsync(Email, "browser-1");

            Assert.True(await sut.IsLoginVerificationSendBlockedAsync(Email));
        }

        [Fact]
        public async Task ActiveCodes_AtCap_AreBlocked_EvenWithCooldownDisabled()
        {
            // Cooldown off so the block can only come from the active-code cap.
            JwtAuthManagerService sut = BuildSut(
                new AuthPolicyOptions { VerificationCodeResendCooldownSeconds = 0, MaxActiveVerificationCodesPerEmail = 2 },
                out _);

            await sut.GenerateAndSaveLoginVerificationCodeAsync(Email, "browser-1");
            Assert.False(await sut.IsLoginVerificationSendBlockedAsync(Email)); // 1 active, below cap

            await sut.GenerateAndSaveLoginVerificationCodeAsync(Email, "browser-1");
            Assert.True(await sut.IsLoginVerificationSendBlockedAsync(Email));  // 2 active, at cap
        }

        [Fact]
        public async Task ExpiredCodes_DoNotCountTowardCooldownOrCap()
        {
            JwtAuthManagerService sut = BuildSut(
                new AuthPolicyOptions { VerificationCodeResendCooldownSeconds = 60, MaxActiveVerificationCodesPerEmail = 1 },
                out InMemoryTokenStorage<LoginVerificationTokenDTO> store);

            // An already-expired code for the same address must be invisible to the policy.
            await store.AddOrUpdateAsync("999999", new LoginVerificationTokenDTO
            {
                Email = Email,
                BrowserId = "browser-1",
                CreatedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            });

            Assert.False(await sut.IsLoginVerificationSendBlockedAsync(Email));
        }

        [Fact]
        public async Task BothLimitsDisabled_NeverBlocks()
        {
            JwtAuthManagerService sut = BuildSut(
                new AuthPolicyOptions { VerificationCodeResendCooldownSeconds = 0, MaxActiveVerificationCodesPerEmail = 0 },
                out _);

            await sut.GenerateAndSaveLoginVerificationCodeAsync(Email, "browser-1");
            await sut.GenerateAndSaveLoginVerificationCodeAsync(Email, "browser-1");

            Assert.False(await sut.IsLoginVerificationSendBlockedAsync(Email));
        }
    }
}
