namespace Spiderly.Security
{
    public static class SettingsProvider
    {
        public static Settings Current { get; set; } = new Settings();
    }

    public class Settings
    {
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

        public string RefreshTokenKey { get; set; } = "refresh_token";
        public string AccessTokenKey { get; set; } = "access_token";
        public string AuthResultKey { get; set; } = "auth_status";
    }
}
