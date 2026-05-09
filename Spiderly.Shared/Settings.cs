using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Emailing;

namespace Spiderly.Shared
{
    public static class SettingsProvider
    {
        public static Settings Current { get; set; } = new Settings();
    }

    public class Settings
    {
        public string ApplicationName { get; set; }
        public string ConnectionString { get; set; }


        public List<string> UnhandledExceptionRecipients { get; set; }
        /// <summary>
        /// Default "From" address for transactional emails. <c>Email</c> is also used as the SMTP
        /// username when the <see cref="EmailingService"/> (SMTP) implementation is active.
        /// </summary>
        public EmailSender EmailSender { get; set; } = new();
        public string EmailSenderPassword { get; set; }
        public string SmtpHost { get; set; } = "smtp.gmail.com";
        public int SmtpPort { get; set; } = 587;
        public string BrevoApiKey { get; set; }

        public string TelegramBotToken { get; set; }
        public string TelegramChatId { get; set; }
        public int NotificationRateLimitMinutes { get; set; } = 5;


        public string JwtKey { get; set; }
        public string JwtIssuer { get; set; } = "https://localhost:7260;";
        public string JwtAudience { get; set; } = "https://localhost:7260;";
        public int ClockSkewMinutes { get; set; } = 1;
        public string AccessTokenKey { get; set; } = "access_token";
        public string RefreshTokenKey { get; set; } = "refresh_token";
        public string AuthResultKey { get; set; } = "auth_status";

        public int RequestsLimitNumber { get; set; } = 240;
        public int RequestsLimitWindow { get; set; } = 60;

        public int BlobUploadRequestsLimitNumber { get; set; } = 20;
        public int BlobUploadRequestsLimitWindow { get; set; } = 60;

        public string S3BucketName { get; set; }
        public string S3PublicEndpoint { get; set; }

        public string CookieDomain { get; set; }
        public SameSiteMode CookieSameSite { get; set; } = SameSiteMode.None;

        public string FrontendUrl { get; set; } = "http://localhost:4200";
        public string ExcelContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public int ExcelExportMaxRows { get; set; } = 100_000;

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