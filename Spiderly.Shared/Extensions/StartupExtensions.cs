﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Azure;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using System.Globalization;
using System.Text;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Resources;
using Microsoft.AspNetCore.Hosting;
using System.Threading.RateLimiting;
using Serilog;
using Serilog.Events;

namespace Spiderly.Shared.Extensions
{
    public static class StartupExtensions
    {
        #region ConfigureServices

        public static void SpiderlyConfigureServices<TDbContext>(this IServiceCollection services) where TDbContext : DbContext, IApplicationDbContext
        {
            services.AddMemoryCache();

            services.SpiderAddAuthentication();

            services.AddAuthorization();

            services.AddHttpContextAccessor();

            services.AddHttpClient();

            services.AddCors();

            services.SpiderlyConfigureCulture(); // It's mandatory to be before AddControllers

            services.SpiderAddControllers();

            services.SpiderAddAzureClients();

            services.SpiderAddDbContext<TDbContext>(); // https://youtu.be/bN57EDYD6M0?si=CVztRqlj0hBSrFXb

            services.SpiderAddSwaggerGen();

            services.AddRateLimiters();
        }

        public static void SpiderAddAuthentication(this IServiceCollection services)
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

        public static void SpiderlyConfigureCulture(this IServiceCollection services)
        {
            services.Configure<RequestLocalizationOptions>(options =>
            {
                CultureInfo[] supportedCultures = new[]
                {
                    new CultureInfo("sr-Latn-RS")
                };

                options.DefaultRequestCulture = new RequestCulture("sr-Latn-RS");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
            });
        }

        public static void SpiderAddControllers(this IServiceCollection services)
        {
            services
                .AddControllers()
                .AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
                    options.JsonSerializerOptions.Converters.Add(new JsonDateTimeConverter());
                });
        }

        public static void SpiderAddAzureClients(this IServiceCollection services)
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

        public static void SpiderAddDbContext<TDbContext>(this IServiceCollection services) where TDbContext : DbContext, IApplicationDbContext
        {
            services.AddDbContext<IApplicationDbContext, TDbContext>(options =>
            {
                options
                    .UseLazyLoadingProxies()
                    .UseSqlServer(SettingsProvider.Current.ConnectionString);

#if DEBUG
                options
                    .LogTo(Console.WriteLine, Microsoft.Extensions.Logging.LogLevel.Information);
#endif
            });
        }

        public static void SpiderAddSwaggerGen(this IServiceCollection services)
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

            app.SpiderlyConfigureCors(); // Allow CORS to connect with the frontend

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

        public static void SpiderlyConfigureCors(this IApplicationBuilder app)
        {
            app.UseCors(builder =>
            {
                builder
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithOrigins(new[] { SettingsProvider.Current.FrontendUrl })
                    .WithExposedHeaders("Content-Disposition"); // to know how to parse the Excel file name on the front end
            });
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
                        string userEmail = Helper.GetCurrentUserEmailOrDefault(context);
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
                            Helper.SendUnhandledExceptionEmails(userEmail, userId, env, ex);
                            message = $"{SharedTerms.GlobalError}";
                            logLevel = LogEventLevel.Error;
                        }

                        Log.Write(
                            logLevel,
                            ex,
                            "Currently authenticated user: {userEmail} (id: {userId});",
                            userEmail, userId
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
