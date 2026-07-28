using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Exceptions;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Default <see cref="IExternalAuthProviderRegistry"/>. For each configured provider it uses a
    /// consumer-registered <see cref="IExternalAuthProvider"/> with a matching <see cref="IExternalAuthProvider.Code"/>
    /// if one exists, otherwise it builds a <see cref="GenericOidcExternalAuthProvider"/> from the config.
    /// <para>
    /// Config validity (codes present + unique, authority resolvable or a custom provider present, client id present,
    /// custom-provider codes present + unique) is enforced at <b>boot</b> by <see cref="ExternalProviderOptionsValidator"/>
    /// via <c>ValidateOnStart</c>. This ctor therefore <b>trusts</b> the options and is a pure builder — it does not
    /// re-validate or throw. See docs/external-auth-providers.md → "Operational lessons".
    /// </para>
    /// </summary>
    public class ExternalAuthProviderRegistry : IExternalAuthProviderRegistry
    {
        private readonly Dictionary<string, IExternalAuthProvider> _providersByCode;
        private readonly List<ExternalProviderPublicInfo> _publicConfigs;

        /// <summary>
        /// Builds the registry from the configured providers and any consumer-registered custom providers.
        /// </summary>
        /// <param name="customProviders">Custom <see cref="IExternalAuthProvider"/> implementations registered in DI.</param>
        /// <param name="options">The bound <see cref="ExternalProviderOptions"/>.</param>
        /// <param name="httpClientFactory">Factory for the HTTP client used by the generic OIDC validator.</param>
        public ExternalAuthProviderRegistry(
            IEnumerable<IExternalAuthProvider> customProviders,
            IOptions<ExternalProviderOptions> options,
            IHttpClientFactory httpClientFactory)
        {
            _providersByCode = new Dictionary<string, IExternalAuthProvider>(StringComparer.OrdinalIgnoreCase);
            _publicConfigs = new List<ExternalProviderPublicInfo>();

            Dictionary<string, IExternalAuthProvider> customByCode = BuildCustomProviderMap(customProviders);

            List<ExternalProviderConfig> configs = options.Value.ExternalProviders ?? new();

            foreach (ExternalProviderConfig config in configs)
            {
                // Advertised via GetExternalProviders (the list a frontend renders dynamic sign-in buttons
                // from), unless the provider opts out because its button is hardcoded in a specific frontend
                // (e.g. a storefront-only id-token provider). Validation below is unaffected either way.
                if (config.ShowInProviderList)
                {
                    _publicConfigs.Add(new ExternalProviderPublicInfo
                    {
                        Code = config.Code,
                        Authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority),
                        ClientId = config.ClientId,
                        Label = config.Label,
                    });
                }

                // A custom provider registered for this code shadows the generic OIDC validator.
                if (customByCode.TryGetValue(config.Code, out IExternalAuthProvider? custom))
                {
                    _providersByCode[config.Code] = custom;
                    continue;
                }

                _providersByCode[config.Code] = BuildGenericProvider(config, httpClientFactory);
            }

            // Custom providers don't require a config entry — register any that weren't already mapped above.
            foreach (KeyValuePair<string, IExternalAuthProvider> entry in customByCode)
            {
                if (_providersByCode.ContainsKey(entry.Key) == false)
                    _providersByCode[entry.Key] = entry.Value;
            }
        }

        /// <inheritdoc/>
        public IExternalAuthProvider Get(string code)
        {
            // A missing/unknown code comes from the client request, so it's a bad request (400 with a
            // machine-readable code) rather than a server fault. Misconfiguration (duplicate codes, missing
            // authority/client id) is caught at boot by ExternalProviderOptionsValidator, never here.
            if (string.IsNullOrWhiteSpace(code))
                throw new BusinessException("External login request is missing a provider code.", ApiErrorCodes.ExternalProviderNotConfigured);

            if (_providersByCode.TryGetValue(code, out IExternalAuthProvider? provider))
                return provider;

            throw new BusinessException($"No external authentication provider is configured for code '{code}'.", ApiErrorCodes.ExternalProviderNotConfigured);
        }

        /// <inheritdoc/>
        public bool IsConfigured(string code) =>
            string.IsNullOrWhiteSpace(code) == false && _providersByCode.ContainsKey(code);

        /// <inheritdoc/>
        public IReadOnlyList<ExternalProviderPublicInfo> GetPublicConfigs() => _publicConfigs;

        private static Dictionary<string, IExternalAuthProvider> BuildCustomProviderMap(IEnumerable<IExternalAuthProvider> customProviders)
        {
            Dictionary<string, IExternalAuthProvider> map = new(StringComparer.OrdinalIgnoreCase);

            // The generic validator is created per-config below, never registered in DI, so anything arriving here is
            // a consumer's own implementation. Non-empty + unique codes are guaranteed by ExternalProviderOptionsValidator.
            foreach (IExternalAuthProvider provider in customProviders ?? Enumerable.Empty<IExternalAuthProvider>())
                map[provider.Code] = provider;

            return map;
        }

        private static GenericOidcExternalAuthProvider BuildGenericProvider(ExternalProviderConfig config, IHttpClientFactory httpClientFactory)
        {
            // Authority resolvability + ClientId presence are guaranteed by ExternalProviderOptionsValidator at boot.
            string authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority)!;
            return new GenericOidcExternalAuthProvider(config.Code, authority, config.ClientId, config.TrustEmailVerified, httpClientFactory.CreateClient());
        }
    }
}
