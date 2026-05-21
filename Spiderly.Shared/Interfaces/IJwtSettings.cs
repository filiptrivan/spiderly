namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the JWT signing/validation settings consumed by the security token codec.
    /// Implemented by <see cref="Settings"/> and injected into services, so token code depends on
    /// configuration passed in rather than the global mutable <c>SettingsProvider</c> static —
    /// which keeps the codec unit-testable and parallel-safe.
    /// </summary>
    public interface IJwtSettings
    {
        /// <summary>Symmetric HMAC-SHA256 signing key for access tokens.</summary>
        string JwtKey { get; }

        /// <summary>Expected token issuer (<c>iss</c>).</summary>
        string JwtIssuer { get; }

        /// <summary>Expected token audience (<c>aud</c>).</summary>
        string JwtAudience { get; }

        /// <summary>Allowed clock drift, in minutes, when validating token issuer/audience/lifetime claims.</summary>
        int ClockSkewMinutes { get; }
    }
}
