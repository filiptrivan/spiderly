namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// The public, non-secret configuration for one enabled external provider, safe to expose to the
    /// browser so a client OIDC library can run the sign-in flow. Projected from <see cref="ExternalProviderConfig"/>
    /// with the authority resolved through <see cref="ExternalProviderPresets"/>.
    /// </summary>
    public class ExternalProviderPublicInfo
    {
        /// <summary>The provider code (e.g. <c>"google"</c>), sent back as the login request's provider.</summary>
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
