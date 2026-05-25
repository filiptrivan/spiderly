using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// Public, non-secret config for one enabled external provider, returned by the security controller so
    /// the frontend can render a sign-in button and run the client OIDC flow. <c>ClientId</c>/<c>Authority</c>
    /// are public by OIDC design.
    /// </summary>
    [SpiderlyDTO]
    public class ExternalProviderPublicDTO
    {
        /// <summary>The provider code (e.g. "google"), sent back as the login request's provider.</summary>
        public string Code { get; set; }

        /// <summary>The OIDC authority / issuer base URL the client runs the flow against.</summary>
        public string Authority { get; set; }

        /// <summary>The public OAuth/OIDC client id.</summary>
        public string ClientId { get; set; }

        /// <summary>Optional display label for the sign-in button.</summary>
        public string Label { get; set; }

        /// <summary>Optional icon URL for the sign-in button.</summary>
        public string IconUrl { get; set; }
    }
}
