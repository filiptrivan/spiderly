namespace Spiderly.Shared
{
    /// <summary>
    /// Cookie / transport key names under which the auth tokens are stored. Bound from the
    /// <c>AppSettings:Spiderly.Shared</c> configuration section and injected as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>. Lives in Spiderly.Shared because both the
    /// Shared-level JWT bearer setup and the Security-level auth/cookie services must agree on these
    /// names — they are a single source of truth.
    /// </summary>
    public class TokenKeyOptions
    {
        /// <summary>Key (cookie / query name) under which the access token is stored.</summary>
        public string AccessTokenKey { get; set; } = "access_token";

        /// <summary>Key (cookie name) under which the refresh token is stored.</summary>
        public string RefreshTokenKey { get; set; } = "refresh_token";

        /// <summary>Key (cookie name) under which the non-HTTP-only auth-result payload is stored.</summary>
        public string AuthResultKey { get; set; } = "auth_status";
    }
}
