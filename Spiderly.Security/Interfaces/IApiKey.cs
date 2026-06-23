namespace Spiderly.Security.Interfaces
{
    /// <summary>
    /// The contract a consumer's API-key entity implements so the framework's
    /// <see cref="Spiderly.Security.Authentication.DefaultApiKeyAuthenticator{TApiKey}"/> can verify a presented
    /// key generically. An API key is a principal (it carries its own roles), so this extends
    /// <see cref="ISecurityPrincipal"/> and adds the credential + lifecycle fields the authenticator checks.
    /// </summary>
    public interface IApiKey : ISecurityPrincipal
    {
        /// <summary>The SHA-256 hash of the key; the plaintext is returned once at generation and never stored.</summary>
        string KeyHash { get; set; }

        /// <summary>When <c>true</c>, the key is revoked and must not authenticate. Treat <c>null</c> as not revoked.</summary>
        bool? IsRevoked { get; set; }

        /// <summary>Optional expiry — a key past this instant must not authenticate. <c>null</c> means it never expires.</summary>
        DateTime? ExpiresAt { get; set; }
    }
}
