namespace Spiderly.Security
{
    /// <summary>
    /// Authentication policy options — token lifetimes, multi-browser / new-IP rules, user provisioning,
    /// and external-provider config. Bound from the <c>AppSettings:Spiderly.Security</c> configuration
    /// section and injected into the security services as
    /// <see cref="Microsoft.Extensions.Options.IOptions{T}"/>, so the security flow depends on
    /// configuration passed in rather than a global mutable static — which keeps the services
    /// unit-testable and parallel-safe.
    /// </summary>
    public class AuthPolicyOptions
    {
        /// <summary>Access token lifetime, in minutes.</summary>
        public int AccessTokenExpiration { get; set; } = 20;

        /// <summary>Refresh token lifetime, in minutes.</summary>
        public int RefreshTokenExpiration { get; set; } = 1440;

        /// <summary>Google OAuth client id used to validate external-provider id tokens.</summary>
        public string GoogleClientId { get; set; }

        /// <summary>
        /// Login verification code lifetime, in minutes. It can be longer — it has the same chance of
        /// being hit as the refresh token — but there is no reason to give it longer; it is effectively
        /// a modified refresh token.
        /// </summary>
        public int VerificationTokenExpiration { get; set; } = 5;

        /// <summary>Number of consecutive failed login attempts that disables a user.</summary>
        public int NumberOfFailedLoginAttemptsInARowToDisableUser { get; set; } = 40;

        /// <summary>When <c>false</c>, a refresh originating from a new IP address invalidates the session.</summary>
        public bool AllowTheUseOfAppWithDifferentIpAddresses { get; set; } = true;

        /// <summary>Maximum number of concurrent browsers (refresh tokens) retained per user.</summary>
        public int AllowedBrowsersForTheSingleUser { get; set; } = 5;

        /// <summary>When <c>true</c>, only an admin may provision new users (no self-registration on first login).</summary>
        public bool OnlyAdminCanAddUsers { get; set; } = false;
    }
}
