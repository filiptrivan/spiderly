namespace Spiderly.Shared
{
    /// <summary>
    /// JWT signing/validation options consumed by the security token codec and the JWT bearer setup.
    /// Bound from the <c>AppSettings:Spiderly.Shared</c> configuration section and injected as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>, so token code depends on configuration
    /// passed in rather than a global mutable static — which keeps the codec unit-testable and parallel-safe.
    /// </summary>
    public class JwtOptions
    {
        /// <summary>Symmetric HMAC-SHA256 signing key for access tokens.</summary>
        public string JwtKey { get; set; }

        /// <summary>Expected token issuer (<c>iss</c>).</summary>
        public string JwtIssuer { get; set; } = "https://localhost:7260;";

        /// <summary>Expected token audience (<c>aud</c>).</summary>
        public string JwtAudience { get; set; } = "https://localhost:7260;";

        /// <summary>Allowed clock drift, in minutes, when validating token issuer/audience/lifetime claims.</summary>
        public int ClockSkewMinutes { get; set; } = 1;
    }
}
