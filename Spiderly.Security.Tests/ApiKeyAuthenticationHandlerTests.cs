using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Security.Authentication;
using Spiderly.Shared.Authorization;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Behaviour tests for <see cref="ApiKeyAuthenticationHandler"/>: it issues an <see cref="PrincipalKinds.ApiKey"/>
    /// principal for an active key, stays out of the way when no key is presented (so JWT can run), and rejects an
    /// invalid key. The key→id lookup is faked; only the handler's hashing + claim shaping is under test.
    /// </summary>
    public class ApiKeyAuthenticationHandlerTests
    {
        private static async Task<AuthenticateResult> AuthenticateAsync(
            string presentedKey,
            Func<string, Task<long?>> resolve,
            string headerName = ApiKeyAuthenticationDefaults.HeaderName)
        {
            ApiKeyAuthenticationOptions options = new() { HeaderName = headerName };
            ApiKeyAuthenticationHandler handler = new(
                new StaticOptionsMonitor<ApiKeyAuthenticationOptions>(options),
                NullLoggerFactory.Instance,
                UrlEncoder.Default,
                new FakeApiKeyAuthenticator(resolve));

            DefaultHttpContext context = new();
            if (presentedKey != null)
                context.Request.Headers[headerName] = presentedKey;

            AuthenticationScheme scheme = new(
                ApiKeyAuthenticationDefaults.AuthenticationScheme, displayName: null, typeof(ApiKeyAuthenticationHandler));
            await handler.InitializeAsync(scheme, context);
            return await handler.AuthenticateAsync();
        }

        [Fact]
        public async Task NoHeader_ReturnsNoResult_SoOtherSchemesCanRun()
        {
            AuthenticateResult result = await AuthenticateAsync(presentedKey: null, resolve: _ => Task.FromResult<long?>(99));

            Assert.True(result.None);
            Assert.False(result.Succeeded);
        }

        [Fact]
        public async Task UnknownOrInactiveKey_Fails()
        {
            AuthenticateResult result = await AuthenticateAsync("deadbeef", resolve: _ => Task.FromResult<long?>(null));

            Assert.False(result.Succeeded);
            Assert.NotNull(result.Failure);
        }

        [Fact]
        public async Task ActiveKey_SucceedsWithApiKeyPrincipal()
        {
            const long apiKeyId = 7;

            AuthenticateResult result = await AuthenticateAsync("the-secret-key", resolve: _ => Task.FromResult<long?>(apiKeyId));

            Assert.True(result.Succeeded);
            ClaimsPrincipal principal = result.Principal!;
            Assert.True(principal.Identity!.IsAuthenticated);
            Assert.Equal(ApiKeyAuthenticationDefaults.AuthenticationScheme, principal.Identity.AuthenticationType);
            Assert.Equal(apiKeyId.ToString(), principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            Assert.Equal(PrincipalKinds.ApiKey, principal.FindFirst(PrincipalClaims.PrincipalKind)?.Value);
        }

        [Fact]
        public async Task Handler_HashesPresentedKey_NeverPassesItRaw()
        {
            const string presented = "the-secret-key";
            string seenByAuthenticator = null;

            await AuthenticateAsync(presented, resolve: hash =>
            {
                seenByAuthenticator = hash;
                return Task.FromResult<long?>(1);
            });

            Assert.Equal(ApiKeyHelper.ComputeSha256Hash(presented), seenByAuthenticator);
            Assert.NotEqual(presented, seenByAuthenticator);
        }

        [Fact]
        public async Task CustomHeaderName_IsHonored()
        {
            AuthenticateResult result = await AuthenticateAsync("k", resolve: _ => Task.FromResult<long?>(1), headerName: "X-My-Key");

            Assert.True(result.Succeeded);
        }
    }

    /// <summary>Single-method fake so a test can script the key-hash → id lookup without a DB.</summary>
    file sealed class FakeApiKeyAuthenticator : IApiKeyAuthenticator
    {
        private readonly Func<string, Task<long?>> _resolve;

        public FakeApiKeyAuthenticator(Func<string, Task<long?>> resolve) => _resolve = resolve;

        public Task<long?> ResolveActiveApiKeyIdAsync(string keyHash) => _resolve(keyHash);
    }

    /// <summary>Minimal <see cref="IOptionsMonitor{T}"/> returning a fixed value, for handler construction in tests.</summary>
    file sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
        where T : class
    {
        private readonly T _value;

        public StaticOptionsMonitor(T value) => _value = value;

        public T CurrentValue => _value;

        public T Get(string name) => _value;

        public IDisposable OnChange(Action<T, string> listener) => null;
    }
}
