using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Helpers;
using Xunit;

namespace Spiderly.Shared.Tests
{
    // Pins the rate-limiter partition rule for machine callers: only a VALIDATED ApiKey principal
    // (stamped by the authentication middleware) earns a per-key partition — an anonymous request or
    // a human principal stays on the per-IP bucket. Keying on the raw API-key header instead would
    // let an attacker mint unlimited fresh buckets and bypass per-IP limiting entirely.
    public class ApiKeyRateLimitPartitionTests
    {
        [Fact]
        public void Anonymous_request_yields_no_api_key_partition()
        {
            DefaultHttpContext httpContext = new();

            Assert.Null(Helper.GetAuthenticatedApiKeyId(httpContext));
        }

        [Fact]
        public void Unauthenticated_identity_with_api_key_claims_yields_no_partition()
        {
            // Claims present but the identity was never authenticated (no authenticationType) —
            // must NOT be trusted as a machine principal.
            DefaultHttpContext httpContext = new();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "5"),
                new Claim(PrincipalClaims.PrincipalKind, PrincipalKinds.ApiKey),
            }));

            Assert.Null(Helper.GetAuthenticatedApiKeyId(httpContext));
        }

        [Fact]
        public void Authenticated_human_principal_yields_no_api_key_partition()
        {
            DefaultHttpContext httpContext = new();
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "42"),
                new Claim(PrincipalClaims.PrincipalKind, PrincipalKinds.User),
            }, authenticationType: "Test"));

            Assert.Null(Helper.GetAuthenticatedApiKeyId(httpContext));
        }

        [Fact]
        public void Authenticated_api_key_principal_yields_its_key_id()
        {
            DefaultHttpContext httpContext = new();
            httpContext.User = TestPrincipals.ApiKey("5");

            Assert.Equal("5", Helper.GetAuthenticatedApiKeyId(httpContext));
        }
    }
}
