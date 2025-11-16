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
using Serilog;
using Serilog.Events;
using Spiderly.Shared.Enums;
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
        /// The SpiderlyConfigureServices method is an extension method for IServiceCollection that centralizes the registration of various services in a .NET application. It performs the following tasks in sequence: <br/>
        /// 1. Adds memory caching. <br/>
        /// 2. Configures JWT-based authentication. <br/>
        /// 3. Adds authorization support. <br/>
        /// 4. Registers HttpContextAccessor and HTTP client services. <br/>
        /// 5. Configures CORS policies. <br/>
        /// 6. Sets up request localization for a specified culture. <br/>
        /// 7. Adds controllers with JSON options. <br/>
        /// 8. Registers Azure Blob Storage clients. <br/>
        /// 9. Configures the application's database context. <br/>
        /// 10.	Adds Swagger for API documentation. <br/>
        /// 11.	Configures rate limiting for requests. <br/>
        /// <br/>
        /// This method simplifies service configuration by consolidating related setup logic into one reusable method. <br/>
        /// </summary>
        public static void SpiderlyConfigureServices<TDbContext>(
            this IServiceCollection services,
            string cultureCode = "en",
            DbProviderCodes dbProvider = DbProviderCodes.SQLServer
        )
            where TDbContext : DbContext, IApplicationDbContext
        {
            services.AddMemoryCache();

            services.SpiderlyAddAuthentication();

            services.AddAuthorization();

            services.AddHttpContextAccessor();

            services.AddHttpClient();

            services.AddCors();

            services.SpiderlyConfigureCulture(cultureCode); // It's mandatory to be before AddControllers

            services.SpiderlyAddControllers();

            services.SpiderlyAddAzureClients();

            services.SpiderlyAddDbContext<TDbContext>(dbProvider); // https://youtu.be/bN57EDYD6M0?si=CVztRqlj0hBSrFXb

            services.SpiderlyAddSwaggerGen();

            services.AddRateLimiters();
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
                        string accessToken = context.Request.Query["access_token"];
                        PathString path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) &&
                            (path.StartsWithSegments("/api/hubs")))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = false,
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
                    options.UseSqlServer(SettingsProvider.Current.ConnectionString);
                }

#if DEBUG
                options.LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
#endif
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
            });
        }

        public static void AddRateLimiters(this IServiceCollection services)
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

        #endregion

        #region Configure

        /// <summary>
        /// Configuring app midlewares
        /// </summary>
        public static void SpiderlyConfigure(this IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.SpiderlyConfigureLocalization();

            app.SpiderlyConfigureSwagger();

            if (env.IsProduction())
            {
                app.UseHttpsRedirection();
            }

            app.SpiderlyConfigureExceptionHandling(env);

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.SpiderlyConfigureEndpoints();
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

                        string message;
                        LogEventLevel logLevel;
                        long? userId = Helper.GetCurrentUserIdOrDefault(context);

                        if (ex is BusinessException businessEx)
                        {
                            context.Response.StatusCode = businessEx.StatusCode;
                            message = businessEx.Message;
                            logLevel = LogEventLevel.Warning;
                        }
                        else if (ex is ExpiredVerificationException expiredVerificationEx)
                        {
                            context.Response.StatusCode = expiredVerificationEx.StatusCode;
                            message = expiredVerificationEx.Message;
                            logLevel = LogEventLevel.Information;
                        }
                        else if (ex is UnauthorizedException unauthorizedEx)
                        {
                            context.Response.StatusCode = unauthorizedEx.StatusCode;
                            message = unauthorizedEx.Message;
                            logLevel = LogEventLevel.Error;
                        }
                        else if (ex is SecurityTokenException securityTokenEx)
                        {
                            context.Response.StatusCode = StatusCodes.Status419AuthenticationTimeout;
                            message = securityTokenEx.Message;
                            logLevel = LogEventLevel.Information;
                        }
                        else
                        {
                            Helper.SendUnhandledExceptionEmails(userId, env, ex);
                            message = $"{SharedTerms.GlobalError}";
                            logLevel = LogEventLevel.Error;
                        }

                        Log.Write(
                            logLevel,
                            ex,
                            "Currently authenticated user id: {userId});",
                            userId
                        );

                        await context.Response.WriteAsJsonAsync(new
                        {
                            StatusCode = context.Response.StatusCode,
                            Message = message,
                            Exception = exceptionString
                        });
                    }
                });
            });
        }

        public static void SpiderlyConfigureEndpoints(this IApplicationBuilder app)
        {
            app.UseRateLimiter();
        }

        #endregion

    }
}
