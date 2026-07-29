using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Spiderly.Shared.ExternalAuth
{
    /// <summary>
    /// Validates id tokens from any standard OpenID Connect provider, configured by
    /// <c>{ Authority, ClientId }</c> alone. Signing keys come from the provider's discovery document
    /// (<c>/.well-known/openid-configuration</c>) via a cached, auto-rotating
    /// <see cref="ConfigurationManager{T}"/>, so adding a standard provider needs no code.
    /// </summary>
    public class GenericOidcExternalAuthProvider : IExternalAuthProvider
    {
        private readonly string _clientId;
        private readonly bool _trustEmailVerified;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configurationManager;
        private static readonly JsonWebTokenHandler _tokenHandler = new();

        /// <inheritdoc/>
        public string Code { get; }

        /// <summary>
        /// Creates a validator for one provider.
        /// </summary>
        /// <param name="code">The provider code (e.g. <c>"google"</c>).</param>
        /// <param name="authority">The OIDC authority / issuer base URL.</param>
        /// <param name="clientId">The client id, validated as the token audience.</param>
        /// <param name="trustEmailVerified">When <c>true</c>, treat a returned email as verified even if the token carries no <c>email_verified</c> claim (for providers like Facebook that verify emails but omit the claim).</param>
        /// <param name="httpClient">HTTP client used to fetch the discovery document and JWKS.</param>
        public GenericOidcExternalAuthProvider(string code, string authority, string clientId, bool trustEmailVerified, HttpClient httpClient)
        {
            Code = code;
            _clientId = clientId;
            _trustEmailVerified = trustEmailVerified;

            string metadataAddress = $"{authority.TrimEnd('/')}/.well-known/openid-configuration";
            _configurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                metadataAddress,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(httpClient) { RequireHttps = true });
        }

        /// <inheritdoc/>
        public async Task<ExternalIdentity> ValidateAsync(string idToken)
        {
            if (string.IsNullOrWhiteSpace(idToken))
                throw new SecurityTokenException("External provider id token is missing.");

            OpenIdConnectConfiguration configuration = await _configurationManager.GetConfigurationAsync();

            TokenValidationParameters validationParameters = new()
            {
                ValidIssuer = configuration.Issuer,
                ValidAudience = _clientId,
                IssuerSigningKeys = configuration.SigningKeys,
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
            };

            TokenValidationResult result = await _tokenHandler.ValidateTokenAsync(idToken, validationParameters);

            if (result.IsValid == false)
                throw result.Exception ?? new SecurityTokenException("External provider id token validation failed.");

            return MapClaimsToIdentity(Code, result.Claims, _trustEmailVerified);
        }

        /// <summary>
        /// Maps a validated token's claims onto an <see cref="ExternalIdentity"/>. Split out from
        /// <see cref="ValidateAsync"/> so the provider-agnostic mapping is unit-testable without a
        /// discovery document, a JWKS, or a signed token — signature validation is Microsoft's concern,
        /// this is ours.
        /// </summary>
        internal static ExternalIdentity MapClaimsToIdentity(
            string code, IDictionary<string, object>? claims, bool trustEmailVerified)
        {
            string? email = GetString(claims, "email");

            return new ExternalIdentity
            {
                Provider = code,
                Subject = GetString(claims, "sub")!,
                Email = email,
                // Providers that verify emails but omit the email_verified claim (e.g. Facebook) opt in via
                // TrustEmailVerified; for everyone else the strict claim check stands.
                EmailVerified = trustEmailVerified ? string.IsNullOrWhiteSpace(email) == false : GetBool(claims, "email_verified"),
                Name = GetString(claims, "name"),
            };
        }

        private static string? GetString(IDictionary<string, object>? claims, string key)
        {
            return claims != null && claims.TryGetValue(key, out object? value) ? value?.ToString() : null;
        }

        private static bool GetBool(IDictionary<string, object>? claims, string key)
        {
            if (claims == null || claims.TryGetValue(key, out object? value) == false || value == null)
                return false;

            // The claim may arrive as a real bool or as the string "true"/"false" depending on the provider.
            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }
    }
}
