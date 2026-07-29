using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Spiderly.Shared.Contracts;
using Spiderly.Shared.Exceptions;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Server-side OAuth Authorization-Code helper for the external-login flow (Option B2): resolves a
    /// provider's OIDC endpoints from its discovery document, builds the authorize URL, and performs the
    /// confidential-client code → token exchange (with the client secret). Singleton — caches one
    /// <see cref="ConfigurationManager{T}"/> per authority. Id-token validation stays in
    /// <see cref="IExternalAuthProvider"/>; this type only obtains the token.
    /// </summary>
    public class ExternalAuthCodeFlow
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ExternalProviderOptions _options;
        private readonly ILogger<ExternalAuthCodeFlow> _logger;
        private readonly IStringLocalizer _localizer;
        private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _configManagers = new(StringComparer.OrdinalIgnoreCase);

        public ExternalAuthCodeFlow(
            IHttpClientFactory httpClientFactory,
            IOptions<ExternalProviderOptions> options,
            ILogger<ExternalAuthCodeFlow> logger,
            IStringLocalizer localizer)
        {
            _httpClientFactory = httpClientFactory;
            _options = options.Value;
            _logger = logger;
            _localizer = localizer;
        }

        /// <summary>
        /// Returns the configured provider for <paramref name="code"/> (authority resolved via preset), or throws
        /// a <see cref="BusinessException"/> with <see cref="ApiErrorCodes.ExternalProviderNotConfigured"/> if absent.
        /// </summary>
        public ExternalProviderConfig GetConfig(string code)
        {
            ExternalProviderConfig? config = (_options.ExternalProviders ?? new())
                .Find(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

            if (config == null)
            {
                // The provider code is a config identifier: useful in a log, meaningless in a toast. The
                // customer gets a localized "try another method"; the operator gets the code here.
                _logger.LogWarning("No external authentication provider is configured for code '{Provider}'.", code);
                throw new BusinessException(_localizer["ExternalProviderNotConfiguredException"], ApiErrorCodes.ExternalProviderNotConfigured);
            }

            return config;
        }

        /// <summary>Builds the provider's authorization-endpoint URL for the code flow (PKCE + nonce + state).</summary>
        public async Task<string> BuildAuthorizeUrlAsync(ExternalProviderConfig config, string state, string nonce, string codeChallenge, string redirectUri)
        {
            OpenIdConnectConfiguration oidc = await GetOidcConfigAsync(config);

            string scopes = string.IsNullOrWhiteSpace(config.Scopes) ? "openid email profile" : config.Scopes;

            string query =
                $"response_type=code" +
                $"&client_id={Uri.EscapeDataString(config.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
                $"&scope={Uri.EscapeDataString(scopes)}" +
                $"&state={Uri.EscapeDataString(state)}" +
                $"&nonce={Uri.EscapeDataString(nonce)}" +
                $"&code_challenge={Uri.EscapeDataString(codeChallenge)}" +
                $"&code_challenge_method=S256";

            string separator = oidc.AuthorizationEndpoint.Contains('?') ? "&" : "?";
            return $"{oidc.AuthorizationEndpoint}{separator}{query}";
        }

        /// <summary>Exchanges the authorization code (+ PKCE verifier + client secret) at the token endpoint and returns the raw id token.</summary>
        public async Task<string> ExchangeCodeForIdTokenAsync(ExternalProviderConfig config, string code, string codeVerifier, string redirectUri)
        {
            OpenIdConnectConfiguration oidc = await GetOidcConfigAsync(config);

            Dictionary<string, string> form = new()
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["client_id"] = config.ClientId,
                // The confidential-client exchange requires a secret; a missing one already fails at
                // request build (null form value) — the '!' preserves that config-error behavior.
                ["client_secret"] = config.ClientSecret!,
                ["code_verifier"] = codeVerifier,
            };

            HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage response = await httpClient.PostAsync(oidc.TokenEndpoint, new FormUrlEncodedContent(form));
            string body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode == false)
            {
                // Surface the provider's error body (e.g. {"error":"invalid_client"}) — it's an OAuth
                // error payload, not a token, so it's safe to log and turns an opaque "400" into a
                // one-line diagnosis. The thrown message stays generic for the client.
                _logger.LogWarning(
                    "External provider '{Provider}' token exchange failed ({StatusCode}): {Body}",
                    config.Code, (int)response.StatusCode, body);

                // A failure HERE is a server-side fault, not a user error: the user already authenticated at
                // the provider, so the auth code is valid — a rejected exchange means OUR config is wrong
                // (bad/empty client secret, redirect mismatch). Throw a non-BusinessException so it maps to
                // 500 (SpiderlyExceptionHandler) and is logged at Error, not dressed up as a 4xx outcome.
                throw new InvalidOperationException($"External provider '{config.Code}' token exchange failed ({(int)response.StatusCode}); see the preceding log for the provider error body.");
            }

            using JsonDocument json = JsonDocument.Parse(body);

            // Same class of fault as the failed-exchange branch above: the user authenticated fine, so a
            // 200 with no usable id_token means OUR config is wrong or the provider is broken. Throw a
            // non-BusinessException so it maps to 500 and logs at Error, rather than surfacing as a 4xx
            // whose message the client renders verbatim. TryGetProperty only proves the property exists,
            // so a literal null is folded into the same guard.
            string? idToken = json.RootElement.TryGetProperty("id_token", out JsonElement idTokenElement)
                ? idTokenElement.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(idToken))
                throw new InvalidOperationException($"External provider '{config.Code}' token response contained no usable id_token.");

            return idToken;
        }

        private async Task<OpenIdConnectConfiguration> GetOidcConfigAsync(ExternalProviderConfig config)
        {
            string? authority = ExternalProviderPresets.ResolveAuthority(config.Code, config.Authority);

            if (string.IsNullOrWhiteSpace(authority))
            {
                _logger.LogWarning("External provider '{Provider}' has no authority configured.", config.Code);
                throw new BusinessException(_localizer["ExternalProviderNotConfiguredException"], ApiErrorCodes.ExternalProviderNotConfigured);
            }

            ConfigurationManager<OpenIdConnectConfiguration> manager = _configManagers.GetOrAdd(authority, a =>
                new ConfigurationManager<OpenIdConnectConfiguration>(
                    $"{a.TrimEnd('/')}/.well-known/openid-configuration",
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(_httpClientFactory.CreateClient()) { RequireHttps = true }));

            return await manager.GetConfigurationAsync();
        }
    }
}
