namespace Spiderly.Shared
{
    /// <summary>
    /// Composition-time-only settings bound from <c>AppSettings:Spiderly.Shared</c> during startup
    /// (the DB connection string, CORS frontend URL, rate-limit and forwarded-headers tuning). These are
    /// read once while building the service collection and are never injected — runtime services consume
    /// the focused <c>*Options</c> classes (e.g. <see cref="JwtOptions"/>, <see cref="S3Options"/>) via
    /// the .NET Options pattern instead.
    /// </summary>
    public class Settings
    {
        /// <summary>Configuration section these settings bind from.</summary>
        public const string ConfigurationSection = "AppSettings:Spiderly.Shared";

        /// <summary>Database connection string used to configure the application <c>DbContext</c>.</summary>
        public string ConnectionString { get; set; }

        /// <summary>Storefront/admin origin allowed by CORS.</summary>
        public string FrontendUrl { get; set; } = "http://localhost:4200";

        /// <summary>Global sliding-window permit limit per IP.</summary>
        public int RequestsLimitNumber { get; set; } = 240;

        /// <summary>Global sliding-window length, in seconds.</summary>
        public int RequestsLimitWindow { get; set; } = 60;

        /// <summary>Blob-upload sliding-window permit limit per IP.</summary>
        public int BlobUploadRequestsLimitNumber { get; set; } = 20;

        /// <summary>Blob-upload sliding-window length, in seconds.</summary>
        public int BlobUploadRequestsLimitWindow { get; set; } = 60;

        /// <summary>
        /// Global sliding-window permit limit for an authenticated API-key principal. Machine callers
        /// (SSR servers, static-site builds, partner integrations) aggregate many end users behind a few
        /// shared egress IPs, so each key gets its own partition with this budget instead of competing
        /// for a per-IP bucket with unrelated clients behind the same egress.
        /// </summary>
        public int ApiKeyRequestsLimitNumber { get; set; } = 1200;

        /// <summary>API-key sliding-window length, in seconds.</summary>
        public int ApiKeyRequestsLimitWindow { get; set; } = 60;

        /// <summary>
        /// Additional CIDR ranges of trusted proxies allowed to set X-Forwarded-For headers
        /// (e.g. <c>["173.245.48.0/20"]</c> for Cloudflare).
        /// RFC 1918 private networks and loopback are always trusted.
        /// </summary>
        public List<string> TrustedProxyNetworks { get; set; } = new();

        /// <summary>
        /// Maximum number of proxy hops to process from X-Forwarded-For.
        /// Set to the number of reverse proxies in front of the app (e.g. 1 for Caddy/Nginx, 2 for Cloudflare + Nginx).
        /// </summary>
        public int ForwardLimit { get; set; } = 1;
    }
}
