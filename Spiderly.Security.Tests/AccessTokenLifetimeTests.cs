using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Localization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Pins that a token <see cref="JwtAuthManagerService"/> issues and the bearer scheme
    /// <c>SpiderlyAddAuthentication</c> configures both read the app's registered <see cref="TimeProvider"/>: the
    /// token's lifetime starts at that clock's now, and the scheme judges it by that clock. Either half on the machine
    /// clock alone breaks sign-in for an app whose clock differs from the machine's, which is every test with a fake
    /// clock. The token is judged by the scheme's own <see cref="TokenValidationParameters"/> through the
    /// <see cref="JsonWebTokenHandler"/> the bearer handler uses.
    /// </summary>
    public class AccessTokenLifetimeTests
    {
        private const string ValidKey = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        [Fact]
        public async Task AnIssuedAccessToken_IsAcceptedUntilTheRegisteredClockPassesItsExpiry()
        {
            // Far from the machine's clock, so reading it instead can never pass by coincidence.
            DateTimeOffset issuedAt = new(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
            FakeTimeProvider clock = new(issuedAt);
            using ServiceProvider provider = BuildServices(clock);
            IJwtAuthManager jwtAuthManager = ActivatorUtilities.CreateInstance<JwtAuthManagerService>(provider);

            JwtAuthResultDTO issued = await jwtAuthManager.GenerateAccessAndRefreshTokensAsync(userId: 42, ipAddress: null, browserId: "browser-1");

            Assert.Equal(issuedAt.UtcDateTime.AddMinutes(20), issued.AccessTokenDTO.ExpiresAt);
            Assert.Equal(issuedAt.UtcDateTime.AddMinutes(1440), issued.RefreshTokenDTO.ExpiresAt);
            Assert.True((await ValidateAsync(provider, issued.AccessTokenDTO.TokenString)).IsValid);

            clock.Advance(TimeSpan.FromMinutes(21));

            TokenValidationResult expired = await ValidateAsync(provider, issued.AccessTokenDTO.TokenString);
            Assert.False(expired.IsValid);
            Assert.IsType<SecurityTokenExpiredException>(expired.Exception);
        }

        private static ServiceProvider BuildServices(TimeProvider clock)
        {
            ServiceCollection services = new();
            services.AddLogging();
            services.AddSingleton<TimeProvider>(clock);
            services.AddSingleton<IStringLocalizer, PassthroughStringLocalizer>();
            services.AddSingleton<ITokenStorage<RefreshTokenDTO>>(new InMemoryTokenStorage<RefreshTokenDTO>(
                new Dictionary<string, Func<RefreshTokenDTO, string?>>
                {
                    { RefreshTokenDTO.UserIdIndex, t => t.UserId.ToString() },
                }));
            services.AddSingleton<ITokenStorage<LoginVerificationTokenDTO>>(new InMemoryTokenStorage<LoginVerificationTokenDTO>());
            services.AddOptions<AuthPolicyOptions>().Configure(o =>
            {
                o.AccessTokenExpiration = 20;
                o.RefreshTokenExpiration = 1440;
            });
            services.AddOptions<JwtOptions>().Configure(o =>
            {
                o.JwtKey = ValidKey;
                o.JwtIssuer = "https://test-issuer";
                o.JwtAudience = "https://test-audience";
                o.ClockSkewMinutes = 0;
            });
            services.SpiderlyAddAuthentication();
            return services.BuildServiceProvider();
        }

        private static Task<TokenValidationResult> ValidateAsync(ServiceProvider provider, string token)
        {
            TokenValidationParameters parameters = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme)
                .TokenValidationParameters;

            return new JsonWebTokenHandler().ValidateTokenAsync(token, parameters);
        }
    }
}
