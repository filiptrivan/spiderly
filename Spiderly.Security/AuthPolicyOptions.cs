namespace Spiderly.Security
{
    /// <summary>
    /// Authentication policy options — token lifetimes, multi-browser / new-IP rules, and user
    /// provisioning. Bound from the <c>AppSettings:Spiderly.Security</c> configuration
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

        /// <summary>
        /// When <c>true</c> (default), an external login whose provider asserts a verified email is automatically
        /// linked to an existing user with that email (and a new user is created when none exists), with no
        /// interstitial. Set <c>false</c> to require explicit linking while signed in — tighten this if the app
        /// later adds password login. Auto-link only ever happens for a verified email; an unverified one is
        /// always rejected regardless of this flag.
        /// </summary>
        public bool AutoLinkByVerifiedEmail { get; set; } = true;

        /// <summary>
        /// Minimum number of seconds between two login-verification emails to the same address. A request
        /// inside this window is silently accepted but sends nothing — the caller already has a fresh code —
        /// so the endpoint cannot be driven to flood an inbox or burn the email provider's quota. The limit
        /// is per-address and IP-independent, so it holds against a distributed sender that the per-IP rate
        /// limiter cannot stop. Set to <c>0</c> to disable the cooldown.
        /// </summary>
        public int VerificationCodeResendCooldownSeconds { get; set; } = 60;

        /// <summary>
        /// Maximum number of simultaneously-valid (unexpired) login-verification codes per address. Once this
        /// many are outstanding, further requests are silently accepted but send nothing until some expire,
        /// bounding how many emails one address can be made to receive within a single
        /// <see cref="VerificationTokenExpiration"/> window. Set to <c>0</c> to disable the cap.
        /// </summary>
        public int MaxActiveVerificationCodesPerEmail { get; set; } = 3;
    }
}
