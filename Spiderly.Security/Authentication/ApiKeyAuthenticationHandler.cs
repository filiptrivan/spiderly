using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Authorization;

namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// Authenticates a request by an API key presented in the configured header (default <c>X-Api-Key</c>).
    /// It hashes the key, resolves it to an active <c>ApiKey</c> id via <see cref="IApiKeyAuthenticator"/>, and
    /// on success issues a ticket whose principal carries the key's id as subject and
    /// <see cref="PrincipalKinds.ApiKey"/> as its <see cref="PrincipalClaims.PrincipalKind"/> claim — so the
    /// principal-kind-agnostic authorization core resolves the key's permissions like any other principal.
    /// Permissions are not resolved here.
    /// </summary>
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        private readonly IApiKeyAuthenticator _authenticator;

        /// <summary>Creates the handler. Constructed per request by the authentication framework.</summary>
        /// <param name="options">Monitor for the scheme's options (the header name).</param>
        /// <param name="logger">The framework logger factory.</param>
        /// <param name="encoder">The framework URL encoder.</param>
        /// <param name="authenticator">The application lookup that resolves a key hash to an active key id.</param>
        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            IApiKeyAuthenticator authenticator)
            : base(options, logger, encoder)
        {
            _authenticator = authenticator;
        }

        /// <inheritdoc/>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string presentedKey = Request.Headers[Options.HeaderName].FirstOrDefault();

            // No key on the request → this scheme has no opinion; let the pipeline fall through (e.g. to JWT).
            if (string.IsNullOrEmpty(presentedKey))
                return AuthenticateResult.NoResult();

            string keyHash = ApiKeyHelper.ComputeSha256Hash(presentedKey);
            long? apiKeyId = await _authenticator.ResolveActiveApiKeyIdAsync(keyHash);

            // Present but unknown / revoked / expired / disabled → a failed authentication attempt (challenge → 401).
            if (apiKeyId == null)
                return AuthenticateResult.Fail("Invalid API key.");

            Claim[] claims =
            {
                new Claim(ClaimTypes.NameIdentifier, apiKeyId.Value.ToString()),
                new Claim(PrincipalClaims.PrincipalKind, PrincipalKinds.ApiKey),
            };

            ClaimsIdentity identity = new(claims, Scheme.Name);
            ClaimsPrincipal principal = new(identity);
            AuthenticationTicket ticket = new(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
}
