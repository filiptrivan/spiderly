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
    /// The map is built eagerly in the constructor so misconfiguration (duplicate codes, missing authority/client id)
    /// fails at startup rather than on the first login.
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

            HashSet<string> seenCodes = new(StringComparer.OrdinalIgnoreCase);

            foreach (ExternalProviderConfig config in configs)
            {
                if (string.IsNullOrWhiteSpace(config.Code))
                    throw new InvalidOperationException("Spiderly: an entry in 'AppSettings:Spiderly.Shared:ExternalProviders' is missing a 'Code'.");

                if (seenCodes.Add(config.Code) == false)
                    throw new InvalidOperationException($"Spiderly: duplicate external provider code '{config.Code}' in 'AppSettings:Spiderly.Shared:ExternalProviders'.");

                // Captured once for the frontend regardless of whether a custom provider shadows the generic one.
                _publicConfigs.Add(new ExternalProviderPublicInfo
                {
                    Code = config.Code,
                    Authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority),
                    ClientId = config.ClientId,
                    Label = config.Label,
                    IconUrl = config.IconUrl,
                });

                // A custom provider registered for this code shadows the generic OIDC validator.
                if (customByCode.TryGetValue(config.Code, out IExternalAuthProvider custom))
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
            // authority/client id) is caught earlier in the constructor and throws at startup.
            if (string.IsNullOrWhiteSpace(code))
                throw new BusinessException("External login request is missing a provider code.", ApiErrorCodes.ExternalProviderNotConfigured);

            if (_providersByCode.TryGetValue(code, out IExternalAuthProvider provider))
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

            foreach (IExternalAuthProvider provider in customProviders ?? Enumerable.Empty<IExternalAuthProvider>())
            {
                // The generic validator is created per-config below, never registered in DI, so anything
                // arriving here is a consumer's own implementation.
                if (string.IsNullOrWhiteSpace(provider.Code))
                    throw new InvalidOperationException($"Spiderly: external auth provider '{provider.GetType().Name}' returns an empty Code.");

                if (map.ContainsKey(provider.Code))
                    throw new InvalidOperationException($"Spiderly: more than one IExternalAuthProvider registered for code '{provider.Code}'.");

                map[provider.Code] = provider;
            }

            return map;
        }

        private static GenericOidcExternalAuthProvider BuildGenericProvider(ExternalProviderConfig config, IHttpClientFactory httpClientFactory)
        {
            string authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority);

            if (string.IsNullOrWhiteSpace(authority))
                throw new InvalidOperationException($"Spiderly: external provider '{config.Code}' has no 'Authority' and no known preset. Set 'Authority' or register a custom IExternalAuthProvider for it.");

            if (string.IsNullOrWhiteSpace(config.ClientId))
                throw new InvalidOperationException($"Spiderly: external provider '{config.Code}' is missing 'ClientId'.");

            return new GenericOidcExternalAuthProvider(config.Code, authority, config.ClientId, httpClientFactory.CreateClient());
        }
    }
}
