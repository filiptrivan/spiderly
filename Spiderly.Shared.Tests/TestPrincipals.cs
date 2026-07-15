using System.Security.Claims;
using Spiderly.Shared.Authorization;

namespace Spiderly.Shared.Tests
{
    // Shared builder for the ClaimsPrincipals the rate-limiter partition tests need, so "how to fake a
    // VALIDATED api-key principal" lives in one place instead of being inlined per test.
    internal static class TestPrincipals
    {
        // A validated api-key principal (authenticationType set, as the auth middleware stamps it).
        public static ClaimsPrincipal ApiKey(string keyId) =>
            new(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, keyId),
                new Claim(PrincipalClaims.PrincipalKind, PrincipalKinds.ApiKey),
            }, authenticationType: "ApiKey"));
    }
}
