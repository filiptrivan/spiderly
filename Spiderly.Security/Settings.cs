namespace Spiderly.Security
{
    public class Settings : Interfaces.IAuthPolicySettings
    {
        /// <summary>Configuration section these settings bind from.</summary>
        public const string ConfigurationSection = "AppSettings:Spiderly.Security";

        public int AccessTokenExpiration { get; set; } = 20;
        public int RefreshTokenExpiration { get; set; } = 1440;

        public string GoogleClientId { get; set; }

        /// <summary>
        /// It can be bigger, it has the same chance of being hit as the refresh token, but there is no reason why we would give it longer
        /// It is actually a modified refresh token
        /// </summary>
        public int VerificationTokenExpiration { get; set; } = 5;
        public int NumberOfFailedLoginAttemptsInARowToDisableUser { get; set; } = 40;
        public bool AllowTheUseOfAppWithDifferentIpAddresses { get; set; } = true;
        public int AllowedBrowsersForTheSingleUser { get; set; } = 5;

        public bool OnlyAdminCanAddUsers { get; set; } = false;

        public bool UseRedisCache { get; set; }
        public string RedisConnectionString { get; set; }

        // Token cookie key names live on Spiderly.Shared.Settings (ITokenKeySettings) as the single
        // source of truth — the JWT bearer middleware (Shared) and the auth/cookie services (Security)
        // must agree on them. Configure them under AppSettings:Spiderly.Shared.
    }
}
