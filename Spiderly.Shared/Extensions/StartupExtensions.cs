using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Resources;
using System.Globalization;
using System.Text;
using System.Threading.RateLimiting;

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
        ///     spiderly.UseAuthentication();
        ///     spiderly.UseExcel();
        ///     spiderly.UseBrevoEmailing();
        ///     spiderly.UseFileStorage&lt;DiskStorageService&gt;();
        ///     spiderly.UseSwagger();
        ///     spiderly.UseRateLimiting();
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
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddHttpClient();
            services.AddCors();
            services.SpiderlyConfigureCulture(builder.CultureCode); // Must be before AddControllers
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

            // Store options for UseSpiderly middleware
            services.AddSingleton(new SpiderlyOptions
            {
                AuthenticationEnabled = builder.AuthenticationEnabled,
                SwaggerEnabled = builder.SwaggerEnabled,
                RateLimitingEnabled = builder.RateLimitingEnabled,
            });

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

                        // SignalR: allow token in query string for hubs only
                        if (path.StartsWithSegments("/api/hubs"))
                        {
                            string accessToken = context.Request.Query[accessTokenKey];

                            if (!string.IsNullOrEmpty(accessToken))
                            {
                                context.Token = accessToken;
                                return Task.CompletedTask;
                            }
                        }

                        // Cookie auth:
                        // JwtBearer will be used by default,
                        // this is used only when Authorization header doesn't exist
                        // E.g. we can use this for Next.js SSR
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

        public static void SpiderlyConfigureCulture(this IServiceCollection services, string cultureCode)
        {
            services.Configure<RequestLocalizationOptions>(options =>
            {
                CultureInfo[] supportedCultures = new[]
                {
                    new CultureInfo(cultureCode)
                };

                options.DefaultRequestCulture = new RequestCulture(cultureCode);
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

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: ipAddress,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = SettingsProvider.Current.RequestsLimitNumber,
                            Window = TimeSpan.FromSeconds(SettingsProvider.Current.RequestsLimitWindow),
                        }
                    );
                });
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
        /// Configures Spiderly middleware. Conditionally activates authentication, Swagger,
        /// and rate limiting based on what was registered via <see cref="AddSpiderly{TDbContext}"/>.
        /// </summary>
        public static void UseSpiderly(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            SpiderlyOptions options = app.ApplicationServices.GetRequiredService<SpiderlyOptions>();

            app.UseMiddleware<RequestIdMiddleware>();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.SpiderlyConfigureLocalization();

            if (options.SwaggerEnabled && env.IsDevelopment())
            {
                app.SpiderlyConfigureSwagger();
            }

            if (env.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            app.SpiderlyConfigureExceptionHandling(env);

            app.UseRouting();

            if (options.AuthenticationEnabled)
            {
                app.UseAuthentication();

                app.UseAuthorization();
            }

            if (options.RateLimitingEnabled)
            {
                app.UseRateLimiter();
            }
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
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Your API V1");
            });
        }

        public static void SpiderlyConfigureExceptionHandling(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseExceptionHandler(appError =>
            {
                appError.Run(async context =>
                {
                    IExceptionHandlerFeature contextFeature = context.Features.Get<IExceptionHandlerFeature>();

                    if (contextFeature != null)
                    {
                        context.Response.ContentType = "application/json";

                        Exception ex = contextFeature.Error;

                        string exceptionString = "";

                        if (env.IsDevelopment())
                            exceptionString = ex.ToString();

                        ILogger logger = context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Spiderly.ExceptionHandler");

                        string message;
                        LogLevel logLevel;
                        long? userId = Helper.GetCurrentUserIdOrDefault(context);

                        if (ex is BusinessException businessEx)
                        {
                            context.Response.StatusCode = businessEx.StatusCode;
                            message = businessEx.Message;
                            logLevel = LogLevel.Warning;
                        }
                        else if (ex is ExpiredVerificationException expiredVerificationEx)
                        {
                            context.Response.StatusCode = expiredVerificationEx.StatusCode;
                            message = expiredVerificationEx.Message;
                            logLevel = LogLevel.Information;
                        }
                        else if (ex is UnauthorizedException unauthorizedEx)
                        {
                            context.Response.StatusCode = unauthorizedEx.StatusCode;
                            message = unauthorizedEx.Message;
                            logLevel = LogLevel.Error;
                        }
                        else if (ex is SecurityTokenException securityTokenEx)
                        {
                            context.Response.StatusCode = StatusCodes.Status419AuthenticationTimeout;
                            message = securityTokenEx.Message;
                            logLevel = LogLevel.Information;

                            CookieHelper.ClearCookie(context.Response.Cookies, SettingsProvider.Current.AccessTokenKey, httpOnly: true);
                            CookieHelper.ClearCookie(context.Response.Cookies, SettingsProvider.Current.RefreshTokenKey, httpOnly: true);
                            CookieHelper.ClearCookie(context.Response.Cookies, SettingsProvider.Current.AuthResultKey, httpOnly: false);
                        }
                        else if (ex is DbUpdateConcurrencyException)
                        {
                            context.Response.StatusCode = StatusCodes.Status409Conflict;
                            message = SharedTerms.ConcurrencyException;
                            logLevel = LogLevel.Warning;
                        }
                        else
                        {
                            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                            message = SharedTerms.GlobalError;
                            logLevel = LogLevel.Error;
                            if (!env.IsDevelopment())
                            {
                                context.RequestServices.GetService<IExceptionNotificationDispatcher>()
                                    ?.DispatchUnhandledException(userId, ex);
                            }
                        }

                        logger.Log(
                            logLevel,
                            ex,
                            "Currently authenticated user id: {userId});",
                            userId
                        );

                        await context.Response.WriteAsJsonAsync(new ApiErrorDTO
                        {
                            StatusCode = context.Response.StatusCode,
                            Message = message,
                            Exception = exceptionString
                        });
                    }
                });
            });
        }

        #endregion

    }
}
