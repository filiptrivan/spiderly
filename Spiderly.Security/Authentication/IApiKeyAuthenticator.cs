namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// The single seam between the framework's API-key authentication and the application's <c>ApiKey</c>
    /// entity. The handler cannot reference the consumer-defined entity, so the application implements this
    /// lookup and registers it via <c>AddSpiderlyApiKeyAuthentication&lt;TAuthenticator&gt;</c>.
    /// </summary>
    public interface IApiKeyAuthenticator
    {
        /// <summary>
        /// Resolves a presented key's hash to the id of the <c>ApiKey</c> principal it identifies, or
        /// <c>null</c> when no <b>active</b> key matches. The implementation MUST exclude keys that are
        /// revoked, expired, or disabled — only an active key may authenticate. The returned id becomes the
        /// authenticated principal's subject, against which the authorization core resolves the key's roles.
        /// </summary>
        /// <param name="keyHash">
        /// The SHA-256 hash (from <see cref="ApiKeyHelper.ComputeSha256Hash"/>) of the key presented in the request header.
        /// </param>
        /// <returns>The active key's id, or <c>null</c> to reject the request.</returns>
        Task<long?> ResolveActiveApiKeyIdAsync(string keyHash);
    }
}
