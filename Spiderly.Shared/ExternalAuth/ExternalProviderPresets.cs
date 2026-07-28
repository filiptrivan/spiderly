using System;
using System.Collections.Generic;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Known-provider presets: well-known provider codes mapped to their OIDC authority, so a consumer
    /// only needs to supply a <c>ClientId</c> for them (the authority is filled in automatically).
    /// Mirrors Spring Security's <c>CommonOAuth2Provider</c> / Auth.js built-ins. Anything not presetted
    /// just supplies its <c>Authority</c> in config.
    /// </summary>
    public static class ExternalProviderPresets
    {
        private static readonly Dictionary<string, string> AuthorityByCode = new(StringComparer.OrdinalIgnoreCase)
        {
            ["google"] = "https://accounts.google.com",
            ["facebook"] = "https://www.facebook.com",
        };

        /// <summary>
        /// Resolves the OIDC authority for a provider: the explicitly configured authority wins, otherwise
        /// the preset for the code is used. Returns null when neither is available.
        /// </summary>
        public static string? ResolveAuthority(string? code, string? configuredAuthority)
        {
            if (string.IsNullOrWhiteSpace(configuredAuthority) == false)
                return configuredAuthority;

            if (code != null && AuthorityByCode.TryGetValue(code, out string? presetAuthority))
                return presetAuthority;

            return null;
        }
    }
}
