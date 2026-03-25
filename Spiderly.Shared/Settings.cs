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
        public string EmailSender { get; set; }
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

        public string BlobStorageConnectionString { get; set; }
        public string BlobStorageUrl { get; set; }
        public string BlobStorageContainerName { get; set; }

        public int RequestsLimitNumber { get; set; } = 240;
        public int RequestsLimitWindow { get; set; } = 60;

        public string CloudinaryCloudName { get; set; }
        public string CloudinaryApiKey { get; set; }
        public string CloudinaryApiSecret { get; set; }

        public string S3BucketName { get; set; }
        public string S3PublicEndpoint { get; set; }

        public string FrontendUrl { get; set; } = "http://localhost:4200";
        public string ExcelContentType { get; set; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

        public int ExcelExportMaxRows { get; set; } = 100_000;

        /// <summary>
        /// CIDR ranges of trusted proxies allowed to set X-Forwarded-For headers (e.g. <c>["10.0.0.0/8"]</c>).
        /// When empty, defaults to RFC 1918 private networks (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16) and loopback.
        /// </summary>
        public List<string> TrustedProxyNetworks { get; set; } = new();

        /// <summary>
        /// Maximum number of proxy hops to process from X-Forwarded-For.
        /// Set to the number of reverse proxies in front of the app (e.g. 1 for Caddy/Nginx, 2 for Cloudflare + Nginx).
        /// </summary>
        public int ForwardLimit { get; set; } = 1;

    }
}