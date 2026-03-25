using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Emailing;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Extensions
{
    /// <summary>
    /// Configures which Spiderly features to enable.
    /// Use <c>Use*</c> for infrastructure choices (database provider, culture)
    /// and <c>Add*</c> for feature registration (auth, emailing, file storage, etc.).
    /// <example>
    /// <code>
    /// services.AddSpiderly&lt;MyDbContext&gt;(spiderly =&gt;
    /// {
    ///     spiderly.UsePostgreSQL();
    ///     spiderly.UseCulture("sr-Latn-RS");
    ///     spiderly.AddAuthentication();
    ///     spiderly.AddExcel();
    ///     spiderly.AddBrevoEmailing();
    ///     spiderly.AddFileStorage&lt;DiskStorageService&gt;();
    ///     spiderly.AddSwagger();
    ///     spiderly.AddRateLimiting();
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public class SpiderlyBuilder
    {
        internal IServiceCollection Services { get; }
        internal DbProviderCodes DbProvider { get; private set; }
        internal bool DbProviderSet { get; private set; }
        internal string CultureCode { get; private set; } = "en";

        internal bool AuthenticationEnabled { get; private set; }
        internal bool ExcelEnabled { get; private set; }
        internal bool SwaggerEnabled { get; private set; }
        internal bool RateLimitingEnabled { get; private set; }
        internal bool ForwardedHeadersEnabled { get; private set; }
        internal bool AzureBlobEnabled { get; private set; }

        internal bool EmailingEnabled { get; private set; }
        internal Type EmailingServiceType { get; private set; }
        internal bool BrevoHttpClientEnabled { get; private set; }

        internal bool FileStorageEnabled { get; private set; }
        internal Type FileStorageServiceType { get; private set; }

        internal SpiderlyBuilder(IServiceCollection services)
        {
            Services = services;
        }

        public SpiderlyBuilder UsePostgreSQL()
        {
            DbProvider = DbProviderCodes.PostgreSQL;
            DbProviderSet = true;
            return this;
        }

        public SpiderlyBuilder UseSQLServer()
        {
            DbProvider = DbProviderCodes.SQLServer;
            DbProviderSet = true;
            return this;
        }

        public SpiderlyBuilder UseCulture(string cultureCode)
        {
            CultureCode = cultureCode;
            return this;
        }

        /// <summary>
        /// Enables JWT-based authentication and authorization middleware.
        /// You still need to register your security services (AuthenticationService, AuthorizationServiceBase,
        /// SecurityServiceBase&lt;TUser&gt;, IJwtAuthManager, ITokenStorage) in your app's service registration.
        /// </summary>
        public SpiderlyBuilder AddAuthentication()
        {
            AuthenticationEnabled = true;
            return this;
        }

        public SpiderlyBuilder AddExcel()
        {
            ExcelEnabled = true;
            return this;
        }

        /// <summary>
        /// Registers the specified type as the <see cref="IEmailingService"/> implementation.
        /// </summary>
        public SpiderlyBuilder AddEmailing<TEmailingService>()
            where TEmailingService : class, IEmailingService
        {
            EmailingEnabled = true;
            EmailingServiceType = typeof(TEmailingService);
            BrevoHttpClientEnabled = false;
            return this;
        }

        /// <summary>
        /// Registers <see cref="BrevoEmailingService"/> as <see cref="IEmailingService"/>
        /// and configures the named "Brevo" HttpClient with the API key from settings.
        /// </summary>
        public SpiderlyBuilder AddBrevoEmailing()
        {
            EmailingEnabled = true;
            EmailingServiceType = typeof(BrevoEmailingService);
            BrevoHttpClientEnabled = true;
            return this;
        }

        /// <summary>
        /// Registers the specified type as the <see cref="IFileManager"/> implementation.
        /// </summary>
        public SpiderlyBuilder AddFileStorage<TFileManager>()
            where TFileManager : class, IFileManager
        {
            FileStorageEnabled = true;
            FileStorageServiceType = typeof(TFileManager);
            return this;
        }

        /// <summary>
        /// Registers Azure Blob Storage clients (BlobServiceClient, BlobContainerClient).
        /// Only needed when using Azure Blob Storage for file management.
        /// </summary>
        public SpiderlyBuilder AddAzureBlobStorage()
        {
            AzureBlobEnabled = true;
            return this;
        }

        public SpiderlyBuilder AddSwagger()
        {
            SwaggerEnabled = true;
            return this;
        }

        public SpiderlyBuilder AddRateLimiting()
        {
            RateLimitingEnabled = true;
            return this;
        }

        /// <summary>
        /// Enables ForwardedHeaders middleware for correct client IP detection behind reverse proxies.
        /// Trusted proxy networks default to RFC 1918 private ranges; override via <c>TrustedProxyNetworks</c> in settings.
        /// <example>
        /// <code>
        /// services.AddSpiderly&lt;MyDbContext&gt;(spiderly =&gt;
        /// {
        ///     spiderly.AddForwardedHeaders();
        /// });
        /// </code>
        /// </example>
        /// </summary>
        public SpiderlyBuilder AddForwardedHeaders()
        {
            ForwardedHeadersEnabled = true;
            return this;
        }
    }
}
