using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Security.DTO
{
    /// <summary>
    /// One-time nonce for the client-side (GIS / id-token) external-login flow, returned by
    /// <c>GetExternalLoginNonce</c>. The SPA passes <see cref="Nonce"/> to the provider's sign-in call
    /// (e.g. Google Identity Services <c>initialize({ nonce })</c>) so it is echoed into the id token's
    /// <c>nonce</c> claim; the backend verifies that echo against a signed copy held in an HttpOnly cookie,
    /// binding the login to this browser and making the id token single-use.
    /// </summary>
    [SpiderlyDTO]
    public class ExternalLoginNonceDTO
    {
        /// <summary>The raw nonce the SPA must pass to the provider sign-in call.</summary>
        public string Nonce { get; set; }
    }
}
