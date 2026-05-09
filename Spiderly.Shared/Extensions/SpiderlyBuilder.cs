using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Spiderly.Shared.Emailing;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Extensions
{
    /// <summary>
    /// Configures which Spiderly features to enable.
    /// Use <c>Use*</c> for infrastructure choices (database provider, culture)
    /// and <c>Add*</c> for feature registration (auth, emailing, etc.).
    /// File storage is selected per-property via <see cref="Spiderly.Shared.Attributes.Entity.StorageAttribute"/>
    /// subclasses (<c>[DiskStorage]</c>, <c>[S3PublicStorage]</c>, <c>[S3PrivateStorage]</c>, or your own
    /// custom subclass); register the implementation classes you reference in your DI container directly
    /// (e.g. <c>services.AddTransient&lt;S3PublicStorageService&gt;()</c>).
    /// <example>
    /// <code>
    /// services.AddSpiderly&lt;MyDbContext&gt;(spiderly =&gt;
    /// {
    ///     spiderly.UsePostgreSQL();
    ///     spiderly.UseCulture("sr-Latn-RS");
    ///     spiderly.AddAuthentication();
    ///     spiderly.AddTokenStorage();
    ///     spiderly.AddExcel();
    ///     spiderly.AddBrevoEmailing();
    ///     spiderly.AddSwagger();
    ///     spiderly.AddRateLimiting();
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public class SpiderlyBuilder
    {
        public IServiceCollection Services { get; }
        internal DbProviderCodes DbProvider { get; private set; }
        internal bool DbProviderSet { get; private set; }
        internal string CultureCode { get; private set; } = "en";
        internal List<string> SupportedCultures { get; private set; } = new() { "en" };
        internal bool TranslationsEnabled { get; private set; }
        internal Type LocalizerType { get; private set; }

        internal bool AuthenticationEnabled { get; private set; }
        internal bool ExcelEnabled { get; private set; }
        internal bool SwaggerEnabled { get; private set; }
        internal bool RateLimitingEnabled { get; private set; }
        internal bool ForwardedHeadersEnabled { get; private set; }

        internal bool EmailingEnabled { get; private set; }
        internal Type EmailingServiceType { get; private set; }
        internal bool BrevoHttpClientEnabled { get; private set; }

        internal SpiderlyBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// Configures the application to use PostgreSQL as the database provider.
        /// </summary>
        public SpiderlyBuilder UsePostgreSQL()
        {
            DbProvider = DbProviderCodes.PostgreSQL;
            DbProviderSet = true;
            return this;
        }

        /// <summary>
        /// Configures the application to use SQL Server as the database provider.
        /// </summary>
        public SpiderlyBuilder UseSQLServer()
        {
            DbProvider = DbProviderCodes.SQLServer;
            DbProviderSet = true;
            return this;
        }

        /// <summary>
        /// Sets the default culture and optionally additional supported cultures.
        /// When multiple cultures are provided, per-request culture resolution is enabled
        /// via Accept-Language header, query string (?culture=X), or cookie.
        /// <example>
        /// <code>
        /// // Single language:
        /// spiderly.UseCulture("sr-Latn-RS");
        ///
        /// // Multi-language:
        /// spiderly.UseCulture("sr-Latn-RS", "en");
        /// </code>
        /// </example>
        /// </summary>
        public SpiderlyBuilder UseCulture(string defaultCulture, params string[] additionalCultures)
        {
            CultureCode = defaultCulture;
            SupportedCultures = new List<string> { defaultCulture };
            SupportedCultures.AddRange(additionalCultures);
            return this;
        }

        /// <summary>
        /// Registers a custom <see cref="IStringLocalizer"/> implementation.
        /// The DI container resolves constructor dependencies automatically.
        /// <example>
        /// <code>
        /// spiderly.UseTranslations&lt;MyDatabaseLocalizer&gt;();
        /// </code>
        /// </example>
        /// </summary>
        public SpiderlyBuilder UseTranslations<TLocalizer>()
            where TLocalizer : class, IStringLocalizer
        {
            LocalizerType = typeof(TLocalizer);
            return this;
        }

        /// <summary>
        /// Registers the built-in JSON file-based localizer.
        /// Loads all {culture}.json files from the <c>Translations</c> directory under <see cref="AppContext.BaseDirectory"/>.
        /// <example>
        /// <code>
        /// spiderly.UseTranslations();
        /// </code>
        /// </example>
        /// </summary>
        public SpiderlyBuilder UseTranslations()
        {
            TranslationsEnabled = true;
            return this;
        }

        /// <summary>
        /// Enables JWT-based authentication and authorization middleware.
        /// You still need to register your security services (AuthenticationService, AuthorizationServiceBase,
        /// SecurityServiceBase&lt;TUser&gt;, IJwtAuthManager) and call <c>spiderly.AddTokenStorage()</c> inside the builder configuration.
        /// </summary>
        public SpiderlyBuilder AddAuthentication()
        {
            AuthenticationEnabled = true;
            return this;
        }

        /// <summary>
        /// Enables Excel export functionality via <see cref="ExcelService"/>.
        /// </summary>
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
        /// Enables Swagger/OpenAPI documentation generation.
        /// </summary>
        public SpiderlyBuilder AddSwagger()
        {
            SwaggerEnabled = true;
            return this;
        }

        /// <summary>
        /// Enables rate limiting middleware with a default sliding window limiter.
        /// </summary>
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
