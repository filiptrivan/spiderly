namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// The normalized result of validating an external provider's id token — provider-agnostic,
    /// so the security layer never depends on any single provider's token shape.
    /// </summary>
    public class ExternalIdentity
    {
        /// <summary>The provider code the identity came from (e.g. <c>"google"</c>).</summary>
        public string Provider { get; set; } = null!;

        /// <summary>
        /// The provider's stable, immutable user identifier (the OIDC <c>sub</c> claim). This is the key
        /// an external login is linked by — it never changes even if the user's email changes.
        /// </summary>
        public string Subject { get; set; } = null!;

        /// <summary>The user's email as asserted by the provider. May be null for providers that don't return one.</summary>
        public string? Email { get; set; }

        /// <summary>
        /// Whether the provider asserts the email is verified. Account auto-link / auto-create is gated on this
        /// being <c>true</c>; standard OIDC providers (Google, Microsoft) always return <c>true</c> for normal accounts.
        /// </summary>
        public bool EmailVerified { get; set; }

        /// <summary>The user's display name as asserted by the provider, if any.</summary>
        public string? Name { get; set; }
    }
}
