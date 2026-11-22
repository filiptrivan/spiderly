using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The WebAPI project's <c>Startup.cs</c>, and the <c>AddAppServices</c> extension it calls: where the app
    /// registers Spiderly's services and middleware. A change to a framework registration updates them
    /// (<c>CODING_STANDARDS.md</c> → "Init template drift").
    /// </summary>
    internal static class StartupTemplate
    {
        internal static SpiderlyFile StartupFile(string appName, DbProviderCodes dbProvider) =>
            new SpiderlyFile { Name = "Startup.cs", Data = GetStartupCsData(appName, dbProvider) };

        internal static SpiderlyFolder ExtensionsFolder(string appName) => new()
        {
            Name = "Extensions",
            Files =
            {
                new SpiderlyFile { Name = "AppServiceExtensions.cs", Data = GetAppServiceExtensionsCsData(appName) },
            }
        };

        private static string GetStartupCsData(string appName, DbProviderCodes dbProvider)
        {
            return $$"""
using Hangfire;
using Hangfire.Dashboard;
using Serilog;
using Spiderly.Shared.Emailing;
using Spiderly.Security.Extensions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Services;
using {{appName}}.WebAPI.Extensions;
using {{appName}}.Infrastructure;
using {{appName}}.Business.Entities;

public class Startup
{
    public IConfiguration Configuration { get; }

    // Composition-time-only Spiderly.Shared settings (connection string, CORS frontend URL). Runtime
    // services consume the focused *Options classes (e.g. JwtOptions) via the .NET Options pattern,
    // registered inside AddSpiderly(Configuration, ...).
    private readonly Spiderly.Shared.Settings _spiderlySharedSettings;

    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
        _spiderlySharedSettings = configuration.GetSection(Spiderly.Shared.Settings.ConfigurationSection)
            .Get<Spiderly.Shared.Settings>() ?? new();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.Configure<{{appName}}.Business.Settings>(Configuration.GetSection({{appName}}.Business.Settings.ConfigurationSection));
        services.Configure<{{appName}}.WebAPI.Settings>(Configuration.GetSection({{appName}}.WebAPI.Settings.ConfigurationSection));

        string spiderlyConnectionString = _spiderlySharedSettings.ConnectionString;
        services.AddHangfire(config =>
            config.{{(dbProvider == DbProviderCodes.SQLServer ? "UseSqlServerStorage(spiderlyConnectionString)" : "UseHangfirePostgreSqlStorage(spiderlyConnectionString)")}}
        );
        services.AddHangfireServer();

        services.AddHealthChecks()
            .AddDbContextCheck<{{appName}}ApplicationDbContext>();

        services.AddSpiderly<{{appName}}ApplicationDbContext>(Configuration, spiderly =>
        {
            spiderly.{{(dbProvider == DbProviderCodes.SQLServer ? "UseSQLServer()" : "UsePostgreSQL()")}};
            spiderly.UseCulture("en");
            spiderly.UseTranslations();
            // One call wires the co-required auth core (current-user/login/JWT, the User principal, and the
            // [AuthGuard(...)] handler forwarded to AuthorizationService) and enables authentication. Add API keys
            // with .AddApiKeys<ApiKey>() if the app exposes them.
            spiderly.AddSecurity<User, UserExternalLogin, {{appName}}.Business.Services.AuthorizationService>();
            spiderly.AddTokenStorage();
            spiderly.AddExcel();
            spiderly.AddEmailing<EmailingService>();
            // File storage is selected per blob property via [DiskStorage], [S3PublicStorage],
            // [S3PrivateStorage], or a custom StorageAttribute subclass. DiskStorageService is
            // pre-registered in AppServiceExtensions; opt in to S3 adapters there when needed.
            // Scaffolded apps are <Nullable>enable</Nullable> and have no deployed API consumers yet, so
            // the spec reflects C# nullability from day one: a required member is `required` and
            // non-nullable in the generated client instead of optional-and-nullable. An existing app
            // adds this as a deliberate contract tightening, on its own deploy.
            spiderly.AddSwagger(options => options.SupportNonNullableReferenceTypes());
            spiderly.AddRateLimiting();
            spiderly.AddForwardedHeaders();
            spiderly.AddOutbox<OutboxMessage>();
            spiderly.AddNotifications(_ => { });
        });

        services.AddAppServices();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.SpiderlyConfigureForwardedHeaders();

        app.UseCors(builder =>
        {
            builder
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials()
                .WithOrigins(new[] { _spiderlySharedSettings.FrontendUrl })
                .WithExposedHeaders("Content-Disposition", RequestIdMiddleware.HeaderName); // Content-Disposition: Excel file name; X-Request-Id: cross-origin JS can read the correlation id
        });

        app.UseMiddleware<RequestIdMiddleware>();

        app.UseSerilogRequestLogging();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
            app.SpiderlyConfigureSwagger();
        }

        if (env.IsProduction())
        {
            app.UseHttpsRedirection();
        }

        app.SpiderlyConfigureLocalization();

        app.SpiderlyConfigureExceptionHandling();

        app.UseRouting();

        // After UseRouting so [IgnoreCsrf] endpoint metadata is resolvable. Global + opt-out by design:
        // a cookie-authenticated write with no X-CSRF header is rejected here, for every endpoint.
        app.UseSpiderlyCsrf();

        app.UseAuthentication();

        app.UseAuthorization();

        app.UseRateLimiter();

        app.SpiderlyUseHangfirePrincipalFilter();

        app.SpiderlyUseOutboxRecurringJob<OutboxMessage>();

        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new LocalRequestsOnlyAuthorizationFilter() },
        });

        app.UseEndpoints(endpoints =>
        {
            endpoints.MapHealthChecks("/health");
            endpoints.MapControllers();
        });
    }
}
""";
        }

        private static string GetAppServiceExtensionsCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Security;
using Spiderly.Security.Extensions;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Services;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Shared.FluentValidation;

namespace {{appName}}.WebAPI.Extensions
{
    public static class AppServiceExtensions
    {
        public static IServiceCollection AddAppServices(this IServiceCollection services)
        {
            #region Spiderly

            // The auth core (current-user/login/JWT, the User principal, and the [AuthGuard(...)] handler forwarded
            // to AuthorizationService) is wired by spiderly.AddSecurity<...>() in Startup; AuthorizationService and its
            // generated base are registered by the generated AddEntityServices(). Only app-specific extras remain here.
            // Narrow current-user slice so domain services depend on the abstraction, not the full HTTP/auth-bound
            // AuthenticationService (registered by AddSecurity). Stateless forwarder.
            services.AddTransient<Spiderly.Shared.Interfaces.ICurrentUserAccessor>(sp => sp.GetRequiredService<AuthenticationService>());

            services.AddSingleton<IConfigureOptions<MvcOptions>, TranslatePropertiesConfiguration>();

            // DiskStorageService is the dev default; add S3PublicStorageService / S3PrivateStorageService here when adopting S3.
            services.AddSingleton<DiskStorageService>();

            #endregion

            #region Business

            services.AddTransient<SecurityService<User, UserExternalLogin>>();
            // AddEntityServices() also registers AuthorizationService and its generated base (the latter forwarded to
            // it) — the generated EntityServiceDependencies injects AuthorizationServiceGenerated.
            services.AddEntityServices();

            #endregion

            return services;
        }
    }
}
""";
        }
    }
}
