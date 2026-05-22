namespace Spiderly.Security
{
    /// <summary>
    /// Composition-time-only settings bound from <c>AppSettings:Spiderly.Security</c> during startup
    /// (the token-storage backend selection). Read once while building the service collection and never
    /// injected — runtime services consume <see cref="AuthPolicyOptions"/> via the .NET Options pattern.
    /// </summary>
    public class Settings
    {
        /// <summary>Configuration section these settings bind from.</summary>
        public const string ConfigurationSection = "AppSettings:Spiderly.Security";

        /// <summary>When <c>true</c>, refresh/verification tokens are stored in Redis instead of in-memory.</summary>
        public bool UseRedisCache { get; set; }

        /// <summary>Redis connection string, used when <see cref="UseRedisCache"/> is enabled.</summary>
        public string RedisConnectionString { get; set; }
    }
}
