using System.Threading.Tasks;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Validates an external provider's id token and projects it onto a provider-agnostic
    /// <see cref="ExternalIdentity"/>. This is the single extension seam for external authentication:
    /// Spiderly ships <see cref="GenericOidcExternalAuthProvider"/> (any OpenID Connect provider, config-only),
    /// and a consumer can register their own implementation for a provider that doesn't follow OIDC
    /// (e.g. Apple's JWT client secret, Facebook's <c>debug_token</c>) — keyed by the same <see cref="Code"/>,
    /// it shadows the generic one.
    /// </summary>
    /// <example>
    /// <code>
    /// public class AppleExternalAuthProvider : IExternalAuthProvider
    /// {
    ///     public string Code => "apple";
    ///     public Task&lt;ExternalIdentity&gt; ValidateAsync(string idToken) { /* Apple-specific validation */ }
    /// }
    /// // Register in the consumer's service setup:
    /// services.AddSingleton&lt;IExternalAuthProvider, AppleExternalAuthProvider&gt;();
    /// </code>
    /// </example>
    public interface IExternalAuthProvider
    {
        /// <summary>The provider code this implementation handles (e.g. <c>"google"</c>). Matched against the login request's provider and the config <see cref="ExternalProviderConfig.Code"/>.</summary>
        string Code { get; }

        /// <summary>
        /// Validates the id token (signature, issuer, audience, expiry, and any provider-specific checks)
        /// and returns the normalized identity. Implementations throw when the token is invalid.
        /// </summary>
        /// <param name="idToken">The id token obtained by the client from the provider.</param>
        Task<ExternalIdentity> ValidateAsync(string idToken);
    }
}
