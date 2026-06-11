using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Options;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Validates <see cref="ExternalProviderOptions"/> at host startup. Wired via
    /// <c>AddOptions&lt;ExternalProviderOptions&gt;().ValidateOnStart()</c> in <c>StartupExtensions</c>, so a
    /// misconfigured provider fails the boot with <b>every</b> problem aggregated — instead of throwing later
    /// from <see cref="ExternalAuthProviderRegistry"/>'s constructor at first resolution, which (for a lazy DI
    /// singleton) can be an unrelated background job at 3 AM rather than a login or startup. This validator owns
    /// the invariants the registry ctor used to throw; the registry now trusts the validated options and only builds.
    /// </summary>
    /// <remarks>
    /// Resolved from DI so it can also inspect the consumer-registered <see cref="IExternalAuthProvider"/>s: a custom
    /// provider keyed by a config's <see cref="ExternalProviderConfig.Code"/> is the escape hatch that lets that
    /// config entry omit an authority/preset (the same rule the registry applies). See
    /// docs/external-auth-providers.md → "Operational lessons".
    /// </remarks>
    public class ExternalProviderOptionsValidator : IValidateOptions<ExternalProviderOptions>
    {
        private readonly IEnumerable<IExternalAuthProvider> _customProviders;

        /// <summary>Creates the validator over the consumer-registered custom providers.</summary>
        /// <param name="customProviders">Custom <see cref="IExternalAuthProvider"/> implementations registered in DI (may be empty).</param>
        public ExternalProviderOptionsValidator(IEnumerable<IExternalAuthProvider> customProviders)
        {
            _customProviders = customProviders;
        }

        /// <inheritdoc/>
        public ValidateOptionsResult Validate(string name, ExternalProviderOptions options)
        {
            List<string> failures = new();

            // Custom providers first: their codes are the escape hatch that excuses a config entry from needing an authority.
            HashSet<string> customCodes = CollectCustomProviderCodes(failures);

            List<ExternalProviderConfig> configs = options?.ExternalProviders ?? new();
            HashSet<string> seenCodes = new(StringComparer.OrdinalIgnoreCase);

            foreach (ExternalProviderConfig config in configs)
            {
                if (string.IsNullOrWhiteSpace(config.Code))
                {
                    failures.Add("Spiderly: an entry in 'AppSettings:Spiderly.Shared:ExternalProviders' is missing a 'Code'.");
                    continue;
                }

                if (seenCodes.Add(config.Code) == false)
                {
                    failures.Add($"Spiderly: duplicate external provider code '{config.Code}' in 'AppSettings:Spiderly.Shared:ExternalProviders'.");
                    continue;
                }

                // A custom provider registered for this code shadows the generic OIDC validator, so it needs no authority/clientId.
                if (customCodes.Contains(config.Code))
                    continue;

                if (string.IsNullOrWhiteSpace(ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority)))
                    failures.Add($"Spiderly: external provider '{config.Code}' has no 'Authority' and no known preset. Set 'Authority' or register a custom IExternalAuthProvider for it.");

                if (string.IsNullOrWhiteSpace(config.ClientId))
                    failures.Add($"Spiderly: external provider '{config.Code}' is missing 'ClientId'.");
            }

            return failures.Count > 0
                ? ValidateOptionsResult.Fail(failures)
                : ValidateOptionsResult.Success;
        }

        private HashSet<string> CollectCustomProviderCodes(List<string> failures)
        {
            HashSet<string> codes = new(StringComparer.OrdinalIgnoreCase);

            foreach (IExternalAuthProvider provider in _customProviders ?? Enumerable.Empty<IExternalAuthProvider>())
            {
                if (string.IsNullOrWhiteSpace(provider.Code))
                {
                    failures.Add($"Spiderly: external auth provider '{provider.GetType().Name}' returns an empty Code.");
                    continue;
                }

                if (codes.Add(provider.Code) == false)
                    failures.Add($"Spiderly: more than one IExternalAuthProvider registered for code '{provider.Code}'.");
            }

            return codes;
        }
    }
}
