using Microsoft.AspNetCore.Authentication;

namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// Options for the API-key authentication scheme. The only knob is the header the key is read from,
    /// defaulting to <see cref="ApiKeyAuthenticationDefaults.HeaderName"/> (<c>X-Api-Key</c>).
    /// </summary>
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        /// <summary>The request header the presented API key is read from. Defaults to <c>X-Api-Key</c>.</summary>
        public string HeaderName { get; set; } = ApiKeyAuthenticationDefaults.HeaderName;
    }
}
