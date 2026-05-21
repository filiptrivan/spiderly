namespace Spiderly.Security.Interfaces
{
    /// <summary>
    /// Read-only view of the authentication policy settings — token lifetimes, multi-browser / new-IP
    /// rules, user provisioning, and external-provider config. Implemented by <see cref="Settings"/> and
    /// injected into services, so the security flow depends on configuration passed in rather than the
    /// global mutable <c>SettingsProvider</c> static — which keeps the services unit-testable and
    /// parallel-safe.
    /// </summary>
    public interface IAuthPolicySettings
    {
        /// <summary>Access token lifetime, in minutes.</summary>
        int AccessTokenExpiration { get; }

        /// <summary>Refresh token lifetime, in minutes.</summary>
        int RefreshTokenExpiration { get; }

        /// <summary>Maximum number of concurrent browsers (refresh tokens) retained per user.</summary>
        int AllowedBrowsersForTheSingleUser { get; }

        /// <summary>When <c>false</c>, a refresh originating from a new IP address invalidates the session.</summary>
        bool AllowTheUseOfAppWithDifferentIpAddresses { get; }

        /// <summary>Login verification code lifetime, in minutes.</summary>
        int VerificationTokenExpiration { get; }

        /// <summary>When <c>true</c>, only an admin may provision new users (no self-registration on first login).</summary>
        bool OnlyAdminCanAddUsers { get; }

        /// <summary>Google OAuth client id used to validate external-provider id tokens.</summary>
        string GoogleClientId { get; }
    }
}
