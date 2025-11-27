namespace Spiderly.Shared
{
    public static class SettingsProvider
    {
        public static Settings Current { internal get; set; } = new Settings();
    }

    public class Settings
    {
        public string ApplicationName { get; set; }
        public string ConnectionString { get; set; }


        public List<string> UnhandledExceptionRecipients { get; set; }
        public string EmailSender { get; set; }
        public string EmailSenderPassword { get; set; }
        public string SmtpHost { get; set; }
        public int SmtpPort { get; set; }


        public string JwtKey { get; set; }
        public string JwtIssuer { get; set; }
        public string JwtAudience { get; set; }
        public int ClockSkewMinutes { get; set; }


        public string BlobStorageConnectionString { get; set; }
        public string BlobStorageUrl { get; set; }
        public string BlobStorageContainerName { get; set; }

        public int RequestsLimitNumber { get; set; }
        public int RequestsLimitWindow { get; set; }
        public string RateLimitingFixedByIpPolicy { get; } = "fixed-by-ip";

        public string SpiderlySecretLicenseToken { get; set; }
        public string SpiderlyPublicLicenseKey { get; } = @"MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEq58peHBU8tzIXs8WEhUVKKWQ6ZadWRAnzm1UwkGEoAIcz0uObTXuBqeh4WvDIRwnqUrhZ0s7wCuuujwH7bm7aw==";

        public string CloudinaryCloudName { get; set; }
        public string CloudinaryApiKey { get; set; }
        public string CloudinaryApiSecret { get; set; }

        public string S3BucketName { get; set; }
        public string S3Endpoint { get; set; }

    }
}