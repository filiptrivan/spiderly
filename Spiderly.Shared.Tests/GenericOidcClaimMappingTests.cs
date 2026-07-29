using Microsoft.IdentityModel.Tokens;
using Spiderly.Shared.ExternalAuth;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// <see cref="ExternalIdentity.Subject"/> is the key an external login is linked by — the stable,
    /// rename-proof half of the trust model that verified-email matching is only a gated fallback for.
    /// <c>sub</c> is REQUIRED by OIDC Core, so a conformant provider always supplies it; these tests pin
    /// that a non-conformant one is rejected at the boundary rather than producing an identity with no key.
    /// </summary>
    public class GenericOidcClaimMappingTests
    {
        private const string Code = "google";

        [Fact]
        public void MapClaimsToIdentity_WhenSubIsAbsent_Throws()
        {
            Dictionary<string, object> claims = new()
            {
                ["email"] = "user@example.com",
                ["email_verified"] = true,
            };

            Assert.Throws<SecurityTokenException>(
                () => GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims, trustEmailVerified: false));
        }

        [Fact]
        public void MapClaimsToIdentity_WhenSubIsLiterallyNull_Throws()
        {
            Dictionary<string, object> claims = new()
            {
                ["sub"] = null!,
                ["email"] = "user@example.com",
            };

            Assert.Throws<SecurityTokenException>(
                () => GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims, trustEmailVerified: false));
        }

        [Fact]
        public void MapClaimsToIdentity_WhenClaimsAreAbsentEntirely_Throws()
        {
            Assert.Throws<SecurityTokenException>(
                () => GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims: null, trustEmailVerified: false));
        }

        [Fact]
        public void MapClaimsToIdentity_WithAConformantToken_MapsEveryClaim()
        {
            Dictionary<string, object> claims = new()
            {
                ["sub"] = "provider-subject-123",
                ["email"] = "user@example.com",
                ["email_verified"] = true,
                ["name"] = "Test User",
            };

            ExternalIdentity identity = GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims, trustEmailVerified: false);

            Assert.Equal(Code, identity.Provider);
            Assert.Equal("provider-subject-123", identity.Subject);
            Assert.Equal("user@example.com", identity.Email);
            Assert.True(identity.EmailVerified);
            Assert.Equal("Test User", identity.Name);
        }

        [Fact]
        public void MapClaimsToIdentity_WithTrustEmailVerified_TreatsAPresentEmailAsVerified()
        {
            // Facebook verifies emails but omits the email_verified claim; the guard must not disturb that opt-in.
            Dictionary<string, object> claims = new()
            {
                ["sub"] = "provider-subject-123",
                ["email"] = "user@example.com",
            };

            ExternalIdentity identity = GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims, trustEmailVerified: true);

            Assert.True(identity.EmailVerified);
        }

        [Fact]
        public void MapClaimsToIdentity_WithTrustEmailVerifiedButNoEmail_IsNotVerified()
        {
            Dictionary<string, object> claims = new()
            {
                ["sub"] = "provider-subject-123",
            };

            ExternalIdentity identity = GenericOidcExternalAuthProvider.MapClaimsToIdentity(Code, claims, trustEmailVerified: true);

            Assert.False(identity.EmailVerified);
        }
    }
}
