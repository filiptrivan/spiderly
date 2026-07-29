using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Localization;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// A token exchange that returns 200 with no usable <c>id_token</c> is a server-side fault, not a user
    /// error: the user already authenticated at the provider, so either our config is wrong or the provider
    /// is broken. It must therefore fail the way the failed-exchange branch above it already does — a
    /// non-BusinessException that maps to 500 and logs at Error — rather than as a 4xx whose raw English
    /// message the interceptor displays verbatim to the customer.
    /// </summary>
    public class ExternalAuthCodeFlowTokenResponseTests
    {
        private const string Authority = "https://idp.test";
        private const string TokenEndpoint = "https://idp.test/token";

        [Fact]
        public async Task ExchangeCodeForIdToken_whenIdTokenIsLiterallyNull_faultsAsAServerError()
        {
            // TryGetProperty proves only that the property exists; a literal null passes it and yields
            // null from GetString().
            ExternalAuthCodeFlow sut = NewSut("""{"id_token": null}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExchangeCodeForIdTokenAsync(NewConfig(), "auth-code", "verifier", "https://app.test/callback"));
        }

        [Fact]
        public async Task ExchangeCodeForIdToken_whenIdTokenIsEmpty_faultsAsAServerError()
        {
            ExternalAuthCodeFlow sut = NewSut("""{"id_token": ""}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExchangeCodeForIdTokenAsync(NewConfig(), "auth-code", "verifier", "https://app.test/callback"));
        }

        [Fact]
        public async Task ExchangeCodeForIdToken_whenIdTokenIsAbsent_faultsAsAServerError()
        {
            ExternalAuthCodeFlow sut = NewSut("""{"access_token": "at"}""");

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => sut.ExchangeCodeForIdTokenAsync(NewConfig(), "auth-code", "verifier", "https://app.test/callback"));
        }

        [Fact]
        public async Task ExchangeCodeForIdToken_withAUsableToken_returnsIt()
        {
            ExternalAuthCodeFlow sut = NewSut("""{"id_token": "the.id.token"}""");

            string idToken = await sut.ExchangeCodeForIdTokenAsync(NewConfig(), "auth-code", "verifier", "https://app.test/callback");

            Assert.Equal("the.id.token", idToken);
        }

        [Fact]
    public void GetConfig_forAnUnconfiguredProvider_throwsALocalizedMessage()
    {
        // BusinessException maps to 400, and the Angular interceptor renders its message verbatim — so a
        // hardcoded English string here is shown to a Serbian customer. BusinessException's own XML doc
        // documents the intended usage as _localizer["Key"].
        ExternalAuthCodeFlow sut = NewSut("{}");

        BusinessException exception = Assert.Throws<BusinessException>(() => sut.GetConfig("not-configured"));

        // The passthrough localizer echoes the key, so this asserts the message went through localization
        // rather than being English prose.
        Assert.Equal("ExternalProviderNotConfiguredException", exception.Message);
        Assert.Equal(ApiErrorCodes.ExternalProviderNotConfigured, exception.ErrorCode);
    }

    [Fact]
    public async Task ExchangeCodeForIdToken_withNoAuthorityConfigured_throwsALocalizedMessage()
    {
        ExternalProviderConfig noAuthority = new() { Code = "no-authority", ClientId = "client-id" };
        ExternalAuthCodeFlow sut = NewSut("{}", noAuthority);

        BusinessException exception = await Assert.ThrowsAsync<BusinessException>(
            () => sut.ExchangeCodeForIdTokenAsync(noAuthority, "auth-code", "verifier", "https://app.test/callback"));

        Assert.Equal("ExternalProviderNotConfiguredException", exception.Message);
        Assert.Equal(ApiErrorCodes.ExternalProviderNotConfigured, exception.ErrorCode);
    }

    [Fact]
    public void ProviderMessages_doNotLeakTheProviderCodeToTheCustomer()
    {
        // The code is a config identifier, useful in a log and meaningless in a toast.
        ExternalAuthCodeFlow sut = NewSut("{}");

        BusinessException exception = Assert.Throws<BusinessException>(() => sut.GetConfig("not-configured"));

        Assert.DoesNotContain("not-configured", exception.Message);
    }

    #region Harness

        private static ExternalProviderConfig NewConfig() => new()
        {
            Code = "test-idp",
            Authority = Authority,
            ClientId = "client-id",
            ClientSecret = "client-secret",
        };

        private static ExternalAuthCodeFlow NewSut(string tokenResponseBody, params ExternalProviderConfig[] extraProviders)
        {
            ExternalProviderOptions options = new()
            {
                ExternalProviders = new List<ExternalProviderConfig> { NewConfig() }.Concat(extraProviders).ToList(),
            };

            return new ExternalAuthCodeFlow(
                new StubHttpClientFactory(new FakeIdpHandler(tokenResponseBody)),
                Options.Create(options),
                NullLogger<ExternalAuthCodeFlow>.Instance,
                new PassthroughStringLocalizer());
        }

        /// <summary>
        /// Serves the two hops the exchange makes — the OIDC discovery document and the token endpoint —
        /// so the real method runs end to end without a network.
        /// </summary>
        private sealed class FakeIdpHandler : HttpMessageHandler
        {
            private readonly string _tokenResponseBody;

            public FakeIdpHandler(string tokenResponseBody) => _tokenResponseBody = tokenResponseBody;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string url = request.RequestUri!.ToString();

                string body = url switch
                {
                    _ when url.Contains(".well-known/openid-configuration") => $$"""
                        {
                          "issuer": "{{Authority}}",
                          "authorization_endpoint": "{{Authority}}/authorize",
                          "token_endpoint": "{{TokenEndpoint}}",
                          "jwks_uri": "{{Authority}}/jwks"
                        }
                        """,
                    _ when url.Contains("/jwks") => """{"keys": []}""",
                    _ => _tokenResponseBody,
                };

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json"),
                });
            }
        }

        private sealed class StubHttpClientFactory : IHttpClientFactory
        {
            private readonly HttpMessageHandler _handler;

            public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

            public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
        }

        #endregion
    }
}
