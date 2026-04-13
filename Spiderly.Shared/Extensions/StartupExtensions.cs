using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using System.Globalization;
using System.Net;
using System.IO;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Spiderly.Shared.Extensions
{
    public static class StartupExtensions
    {
        #region ConfigureServices

        /// <summary>
        /// Registers Spiderly framework services using a modular builder pattern.
        /// Only the features you opt in to via the builder are registered.
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
        ///     spiderly.AddFileStorage&lt;DiskStorageService&gt;();
        ///     spiderly.AddSwagger();
        ///     spiderly.AddRateLimiting();
        /// });
        /// </code>
        /// </example>
        /// </summary>
        public static SpiderlyBuilder AddSpiderly<TDbContext>(
            this IServiceCollection services,
            Action<SpiderlyBuilder> configure
        )
            where TDbContext : DbContext, IApplicationDbContext
        {
            SpiderlyBuilder builder = new(services);
            configure(builder);

            if (!builder.DbProviderSet)
            {
                throw new InvalidOperationException(
                    "A database provider must be configured. Call UsePostgreSQL() or UseSQLServer() inside the AddSpiderly configuration.");
            }

            // Core (always registered)
            services.AddExceptionHandler<SpiderlyExceptionHandler>();
            services.AddProblemDetails(); // Required alongside AddExceptionHandler<T> so UseExceptionHandler() passes its options check
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddHttpClient();
            services.AddCors();
            services.SpiderlyConfigureCulture(builder); // Must be before AddControllers
            services.SpiderlyAddControllers();
            services.SpiderlyAddDbContext<TDbContext>(builder.DbProvider);

            // Optional modules
            if (builder.AuthenticationEnabled)
            {
                services.SpiderlyAddAuthentication();
                services.AddAuthorization();
            }

            if (builder.ExcelEnabled)
            {
                services.AddTransient<ExcelService>();
            }

            if (builder.EmailingEnabled)
            {
                services.AddTransient(typeof(IEmailingService), builder.EmailingServiceType);

                if (builder.BrevoHttpClientEnabled)
                {
                    services.SpiderlyAddBrevoHttpClient();
                }
            }

            if (builder.FileStorageEnabled)
            {
                services.AddTransient(typeof(IFileManager), builder.FileStorageServiceType);
            }

            if (builder.AzureBlobEnabled)
            {
                services.SpiderlyAddAzureClients();
            }

            if (builder.SwaggerEnabled)
            {
                services.SpiderlyAddSwaggerGen();
            }

            if (builder.RateLimitingEnabled)
            {
                services.SpiderlyAddRateLimiters();
            }

            if (builder.ForwardedHeadersEnabled)
            {
                services.SpiderlyAddForwardedHeaders();
            }

            if (builder.LocalizerType != null)
            {
                services.AddSingleton(typeof(IStringLocalizer), builder.LocalizerType);
            }
            else if (builder.TranslationsEnabled)
            {
                services.AddSingleton<IStringLocalizer, Localization.JsonStringLocalizer>();
            }
            else
            {
                services.AddSingleton<IStringLocalizer, Localization.PassthroughStringLocalizer>();
            }

            return builder;
        }

        public static void SpiderlyAddAuthentication(this IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        PathString path = context.HttpContext.Request.Path;

                        string accessTokenKey = SettingsProvider.Current.AccessTokenKey;

                        // SignalR clients can't set HTTP headers, so hubs accept the token via query string
                        if (path.StartsWithSegments("/api/hubs"))
                        {
                            string accessToken = context.Request.Query[accessTokenKey];

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                                return Task.CompletedTask;
                            }
                        }

                        // SSR frameworks (e.g. Next.js) can't set Authorization headers on server-side requests, so fall back to cookie
                        if (string.IsNullOrEmpty(context.Token))
                        {
                            if (context.Request.Cookies.TryGetValue(accessTokenKey, out string cookieToken) &&
                                !string.IsNullOrWhiteSpace(cookieToken))
                            {
                                context.Token = cookieToken;
                                return Task.CompletedTask;
                            }
                        }

                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true, // GetTokenAsync() returns null for rejected tokens, so code that needs the raw token uses Helper.GetAccessTokenFromHeader/Cookie() instead
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = SettingsProvider.Current.JwtIssuer,
                    ValidAudience = SettingsProvider.Current.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SettingsProvider.Current.JwtKey)),
                    ClockSkew = TimeSpan.FromMinutes(SettingsProvider.Current.ClockSkewMinutes),
                };
            });
        }

        public static void SpiderlyConfigureCulture(this IServiceCollection services, SpiderlyBuilder builder)
        {
            CultureInfo[] supportedCultures = builder.SupportedCultures
                .Select(c => new CultureInfo(c))
                .ToArray();

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture(builder.CultureCode);
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
        }

        public static void SpiderlyAddControllers(this IServiceCollection services)
        {
            services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
                    options.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter());
                });
        }

        public static void SpiderlyAddAzureClients(this IServiceCollection services)
        {
            if (string.IsNullOrEmpty(SettingsProvider.Current.BlobStorageConnectionString))
                return;

            services.AddAzureClients(clientBuilder =>
            {
                clientBuilder.AddBlobServiceClient(SettingsProvider.Current.BlobStorageConnectionString);

                clientBuilder.AddClient<BlobContainerClient, BlobClientOptions>((options, provider) => // https://stackoverflow.com/questions/78430531/registering-blobcontainerclient-and-injecting-into-isolated-function
                {
                    string storageContainerName = SettingsProvider.Current.BlobStorageContainerName;

                    BlobServiceClient blobServiceClient = provider.GetRequiredService<BlobServiceClient>();

                    BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(storageContainerName);

                    return blobContainerClient;
                });
            });
        }

        public static void SpiderlyAddDbContext<TDbContext>(this IServiceCollection services, DbProviderCodes dbProvider) where TDbContext : DbContext, IApplicationDbContext
        {
            services.AddDbContext<IApplicationDbContext, TDbContext>(options =>
            {
                options.UseLazyLoadingProxies();

                if (dbProvider == DbProviderCodes.SQLServer)
                {
                    options.UseSqlServer(SettingsProvider.Current.ConnectionString);
                }
                else if (dbProvider == DbProviderCodes.PostgreSQL)
                {
                    options.UseNpgsql(SettingsProvider.Current.ConnectionString);
                }

            });
        }

        public static void SpiderlyAddSwaggerGen(this IServiceCollection services)
        {
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "WebAPI",
                    Version = "v1"
                });

                options.DocumentFilter<ErrorResponseFilter>();

                string basePath = AppContext.BaseDirectory;
                foreach (string xmlFile in Directory.GetFiles(basePath, "*.xml"))
                {
                    try { options.IncludeXmlComments(xmlFile); }
                    catch (InvalidOperationException) { }
                }
            });
        }

        public static void SpiderlyAddRateLimiters(this IServiceCollection services)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    string ipAddress = Helper.GetIPAddress(httpContext);

                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: ipAddress,
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = SettingsProvider.Current.RequestsLimitNumber,
                            Window = TimeSpan.FromSeconds(SettingsProvider.Current.RequestsLimitWindow),
                            SegmentsPerWindow = 6,
                        }
                    );
                });

                options.OnRejected = (context, cancellationToken) =>
                {
                    HttpContext httpContext = context.HttpContext;
                    string ip = Helper.GetIPAddress(httpContext) ?? "unknown";
                    string path = httpContext.Request.Path;
                    string method = httpContext.Request.Method;

                    string policyName = "Global";
                    EnableRateLimitingAttribute rateLimitAttr = httpContext.GetEndpoint()
                        ?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
                    if (rateLimitAttr != null)
                    {
                        policyName = rateLimitAttr.PolicyName;
                    }

                    ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Spiderly.RateLimiting");
                    logger.LogWarning(
                        "Rate limit rejected: Policy={Policy}, IP={IP}, Method={Method}, Path={Path}",
                        policyName, ip, method, path);

                    INotificationDispatcher dispatcher = httpContext.RequestServices
                        .GetService<INotificationDispatcher>();
                    dispatcher?.DispatchSecurityEvent(
                        "Rate Limit Rejection",
                        $"ratelimit:{policyName}",
                        $"Policy: {policyName}\nIP: {ip}\nMethod: {method}\nPath: {path}\nTime: {DateTimeOffset.UtcNow:O}");

                    return ValueTask.CompletedTask;
                };
            });
        }

        public static void SpiderlyAddForwardedHeaders(this IServiceCollection services)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = SettingsProvider.Current.ForwardLimit;

                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                // Always trust RFC 1918 private networks + loopback (covers Docker, k8s, local reverse proxies)
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Loopback, 8));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.IPv6Loopback, 128));

                // Add any additional trusted proxy networks (e.g. Cloudflare, AWS CloudFront, etc.)
                foreach (string network in SettingsProvider.Current.TrustedProxyNetworks)
                {
                    string[] parts = network.Split('/');

                    if (parts.Length != 2
                        || !IPAddress.TryParse(parts[0], out IPAddress address)
                        || !int.TryParse(parts[1], out int prefixLength))
                    {
                        throw new InvalidOperationException(
                            $"Invalid CIDR notation in TrustedProxyNetworks: '{network}'. Expected format: 'ip/prefix' (e.g. '10.0.0.0/8').");
                    }

                    options.KnownNetworks.Add(new IPNetwork(address, prefixLength));
                }
            });
        }

        /// <summary>
        /// Registers a named <c>"Brevo"</c> HttpClient pre-configured with the Brevo API base address
        /// and the API key from <see cref="SettingsProvider.Current"/>.
        /// </summary>
        public static void SpiderlyAddBrevoHttpClient(this IServiceCollection services)
        {
            services.AddHttpClient("Brevo", client =>
            {
                client.BaseAddress = new Uri("https://api.brevo.com/v3/");
                client.DefaultRequestHeaders.Add("api-key", SettingsProvider.Current.BrevoApiKey);
            });
        }

        #endregion

        #region Configure

        /// <summary>
        /// Adds ForwardedHeaders middleware to process X-Forwarded-For and X-Forwarded-Proto headers from trusted proxies.
        /// Must be called early in the pipeline — before CORS, authentication, rate limiting, and exception handling.
        /// </summary>
        public static void SpiderlyConfigureForwardedHeaders(this IApplicationBuilder app)
        {
            app.UseForwardedHeaders();
        }

        public static void SpiderlyConfigureLocalization(this IApplicationBuilder app)
        {
            RequestLocalizationOptions localizationOptions = app.ApplicationServices
                .GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;

            app.UseRequestLocalization(localizationOptions);
        }

        public static void SpiderlyConfigureSwagger(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1");
            });
        }

        public static void SpiderlyConfigureExceptionHandling(this IApplicationBuilder app)
        {
            app.UseExceptionHandler();
        }

        #endregion

    }
}
