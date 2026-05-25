using System.Collections.Generic;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Resolves a provider code to the <see cref="IExternalAuthProvider"/> that validates its tokens.
    /// Built once at startup from the configured <see cref="ExternalProviderConfig"/> list plus any
    /// consumer-registered custom providers.
    /// </summary>
    public interface IExternalAuthProviderRegistry
    {
        /// <summary>
        /// Returns the provider registered for <paramref name="code"/>, or throws when no provider is configured for it.
        /// </summary>
        IExternalAuthProvider Get(string code);

        /// <summary>
        /// Whether a provider is configured for <paramref name="code"/>. Non-throwing — lets callers produce a
        /// localized message before resolving with <see cref="Get"/>.
        /// </summary>
        bool IsConfigured(string code);

        /// <summary>
        /// The public, non-secret config for every configured provider — for the frontend to render sign-in
        /// buttons and run the client OIDC flow. One entry per configured <see cref="ExternalProviderConfig"/>.
        /// </summary>
        IReadOnlyList<ExternalProviderPublicInfo> GetPublicConfigs();
    }
}
