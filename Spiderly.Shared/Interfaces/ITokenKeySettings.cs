namespace Spiderly.Shared.Interfaces
{
    /// <summary>
    /// Read-only view of the cookie / transport key names under which the auth tokens are stored.
    /// Implemented by <see cref="Settings"/> and injected into services, so cookie handling depends on
    /// configuration passed in rather than the global mutable <c>SettingsProvider</c> static.
    /// Lives in Spiderly.Shared because both the Shared-level JWT bearer setup and the Security-level
    /// auth/cookie services must agree on these names — they are a single source of truth.
    /// </summary>
    public interface ITokenKeySettings
    {
        /// <summary>Key (cookie / query name) under which the access token is stored.</summary>
        string AccessTokenKey { get; }

        /// <summary>Key (cookie name) under which the refresh token is stored.</summary>
        string RefreshTokenKey { get; }

        /// <summary>Key (cookie name) under which the non-HTTP-only auth-result payload is stored.</summary>
        string AuthResultKey { get; }
    }
}
