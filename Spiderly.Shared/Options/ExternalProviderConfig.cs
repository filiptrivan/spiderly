namespace Spiderly.Shared
{
    /// <summary>
    /// Configuration for a single external authentication provider, bound from the
    /// <c>AppSettings:Spiderly.Shared:ExternalProviders</c> array. One entry enables one provider
    /// (Google, Microsoft/Entra, Auth0, Keycloak, a corporate SSO, …).
    /// </summary>
    /// <remarks>
    /// For standard OpenID Connect providers this is all the configuration required — Spiderly's
    /// generic OIDC validator resolves the signing keys from the provider's discovery document.
    /// A consumer adding a non-OIDC provider registers their own <see cref="Spiderly.Shared.ExternalAuth.IExternalAuthProvider"/>
    /// keyed by the same <see cref="Code"/>, which shadows the generic one.
    /// </remarks>
    public class ExternalProviderConfig
    {
        /// <summary>
        /// Stable string identifier for the provider (e.g. <c>"google"</c>, <c>"microsoft"</c>, or a
        /// consumer-defined code). Used as the routing key on the login request and as the
        /// <c>Provider</c> value stored against the user. Must be unique across the list.
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// The provider's OIDC authority / issuer base URL (e.g. <c>https://accounts.google.com</c>),
        /// from which the discovery document and JWKS are fetched. Optional when <see cref="Code"/>
        /// matches a known preset (see <see cref="Spiderly.Shared.ExternalAuth.ExternalProviderPresets"/>),
        /// which supplies the authority automatically.
        /// </summary>
        public string Authority { get; set; }

        /// <summary>
        /// The OAuth/OIDC client id issued by the provider for this application. Validated as the
        /// token audience. Public by OIDC design (safe to expose to the frontend).
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// The OAuth client secret, used **only server-side** for the authorization-code → token exchange
        /// (confidential-client flow). **Never** exposed to the frontend. Store via gitignored local secrets /
        /// environment variables, never committed.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>Space-separated OAuth scopes requested at sign-in. Defaults to <c>"openid email profile"</c> when empty.</summary>
        public string Scopes { get; set; }

        /// <summary>Optional display label for the provider's sign-in button (e.g. <c>"Continue with Google"</c>).</summary>
        public string Label { get; set; }

        /// <summary>
        /// When <c>true</c>, the provider's returned email is treated as verified even if the id token carries
        /// no <c>email_verified</c> claim — the generic validator sets
        /// <see cref="Spiderly.Shared.ExternalAuth.ExternalIdentity.EmailVerified"/> to <c>true</c> whenever an
        /// email is present. Set this <b>only</b> for providers you trust to verify the emails they return but
        /// which omit the claim (e.g. Facebook). Defaults to <c>false</c>, keeping the strict
        /// <c>email_verified</c> check. The security layer's auto-link/provision gate relies on this flag, so
        /// enabling it for an IdP that does not actually verify emails would weaken account-takeover protection.
        /// </summary>
        public bool TrustEmailVerified { get; set; }

        /// <summary>
        /// Whether this provider is advertised via <c>GET /api/security/GetExternalProviders</c> — the list a
        /// frontend renders its dynamic, config-driven sign-in buttons from (e.g. an authorization-code-flow
        /// admin UI). Defaults to <c>true</c>. Set to <c>false</c> for a provider whose sign-in button is
        /// hardcoded in a specific frontend (e.g. a storefront-only id-token provider): the provider is still
        /// registered and its tokens are still validated, it simply does not appear in the public providers
        /// list — so a frontend driven by that list won't render an unusable button for it.
        /// </summary>
        public bool ShowInProviderList { get; set; } = true;
    }
}
