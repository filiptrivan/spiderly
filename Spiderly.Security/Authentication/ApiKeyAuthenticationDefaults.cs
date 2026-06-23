namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// Well-known names for the API-key authentication scheme.
    /// </summary>
    public static class ApiKeyAuthenticationDefaults
    {
        /// <summary>The authentication scheme name the API-key handler is registered under.</summary>
        public const string AuthenticationScheme = "ApiKey";

        /// <summary>
        /// The forwarding policy scheme installed as the application default when API-key authentication is
        /// enabled. It routes each request to <see cref="AuthenticationScheme"/> when the API-key header is
        /// present, otherwise to JWT bearer — so endpoints accept either credential with no per-endpoint change.
        /// </summary>
        public const string PolicyScheme = "Spiderly:ApiKeyOrJwt";

        /// <summary>The default request header carrying the API key.</summary>
        public const string HeaderName = "X-Api-Key";
    }
}
