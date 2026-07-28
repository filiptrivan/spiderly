using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Spiderly.Shared.Authorization;
using Spiderly.Shared.Constants;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Excel;
using Spiderly.Shared.ExternalAuth;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.IntegrationEvents;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Outbox;
using Spiderly.Shared.RateLimiting;
using Spiderly.Shared.Security;
using Spiderly.Shared.Services;
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
        /// File storage adapters are not configured here — they are selected per blob property
        /// via <see cref="Spiderly.Shared.Attributes.Entity.StorageAttribute"/> subclasses, and
        /// the consumer registers each implementation class it references in DI directly.
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
        public static SpiderlyBuilder AddSpiderly<TDbContext>(
            this IServiceCollection services,
            IConfiguration configuration,
            Action<SpiderlyBuilder> configure
        )
            where TDbContext : DbContext, IApplicationDbContext
        {
            SpiderlyBuilder builder = new(services, configuration);
            configure(builder);

            if (!builder.DbProviderSet)
            {
                throw new InvalidOperationException(
                    "A database provider must be configured. Call UsePostgreSQL() or UseSQLServer() inside the AddSpiderly configuration.");
            }

            // Bind the focused settings classes from the Spiderly.Shared section via the Options pattern.
            // Framework services inject IOptions<T> rather than depending on a global mutable static, and
            // ValidateOnStart fails fast at boot. The signing-key check is conditional: a JWT key is only
            // required when authentication is enabled.
            IConfigurationSection section = configuration.GetSection(Settings.ConfigurationSection);

            services.AddOptions<JwtOptions>().Bind(section)
                .Validate(
                    o => !builder.AuthenticationEnabled || !string.IsNullOrWhiteSpace(o.JwtKey),
                    $"Spiderly: '{Settings.ConfigurationSection}:JwtKey' is required when authentication is enabled.")
                .ValidateOnStart();
            services.AddOptions<TokenKeyOptions>().Bind(section).ValidateOnStart();
            services.AddOptions<S3Options>().Bind(section).ValidateOnStart();
            services.AddOptions<EmailOptions>().Bind(section)
                .Validate(
                    o => !builder.EmailingEnabled || !string.IsNullOrWhiteSpace(o.EmailSender?.Email),
                    // Fail loud at boot rather than 500 on the first send. The 'string vs object' note is here
                    // because a string-typed 'EmailSender' silently binds to an empty object (Email == null).
                    $"Spiderly: '{Settings.ConfigurationSection}:EmailSender:Email' is required when emailing is enabled. " +
                    $"'EmailSender' must be an OBJECT (e.g. {{ \"Email\": \"you@example.com\", \"Name\": \"...\" }}), not a string.")
                .ValidateOnStart();
            services.AddOptions<NotificationOptions>().Bind(section).ValidateOnStart();
            // Optional outbox retry tuning, nested under Spiderly.Shared:Outbox. Every value is optional (falls back to
            // the handler's code-declared RetryPolicy / OutboxRetryPolicy.Default); the guard rejects nonsense overrides
            // (e.g. MaxAttempts 0 would dead-letter every row on first failure) loudly at boot rather than silently.
            static bool SaneOutboxRetry(OutboxRetryOptions r)
                => r == null || ((r.MaxAttempts is null or >= 1) && (r.MaxBackoffMinutes is null or >= 1));
            services.AddOptions<OutboxOptions>().Bind(section.GetSection("Outbox"))
                .Validate(
                    o => o.RetentionDays >= 1 && o.BacklogAgeAlertMinutes >= 1
                         && SaneOutboxRetry(o.Default) && (o.Handlers == null || o.Handlers.Values.All(SaneOutboxRetry)),
                    $"Spiderly: '{Settings.ConfigurationSection}:Outbox' needs RetentionDays >= 1, BacklogAgeAlertMinutes >= 1, and any retry override's MaxAttempts/MaxBackoffMinutes >= 1.")
                .ValidateOnStart();
            services.AddOptions<CookieSettings>().Bind(section).ValidateOnStart();
            services.AddOptions<ExcelOptions>().Bind(section).ValidateOnStart();
            // External-provider config is validated at boot (aggregated) by ExternalProviderOptionsValidator, so a
            // misconfig fails startup rather than lazily from the registry ctor. See docs → "Operational lessons".
            services.AddOptions<ExternalProviderOptions>().Bind(section).ValidateOnStart();
            services.AddSingleton<IValidateOptions<ExternalProviderOptions>, ExternalProviderOptionsValidator>();
            services.AddOptions<Settings>().Bind(section).ValidateOnStart();

            // Composition-time-only values (connection string, rate-limit/proxy tuning) plus the Email
            // snapshot the Brevo HttpClient setup needs before the container is built. JWT/auth options are
            // consumed via IOptions<T> inside SpiderlyAddAuthentication, so no local snapshot is needed.
            Settings settings = section.Get<Settings>() ?? new();
            EmailOptions email = section.Get<EmailOptions>() ?? new();

            // Core (always registered)
            services.AddSingleton<CookieManager>();

            // Empty-safe principal registry, built from any AddSpiderlyPrincipal<T>() registrations (none →
            // resolves to nothing). Registered unconditionally so authorization resolves even if a consumer
            // wires authz services without enabling the full authentication module. TryAdd lets a consumer
            // override it with a custom implementation.
            services.TryAddSingleton<IPrincipalRegistry, PrincipalRegistry>();

            // Splits the two identity questions ("who is calling" vs "which person") so a machine principal
            // can never be read as a user id. Singleton: pure, holds only the registry.
            services.TryAddSingleton<PrincipalIdentity>();

            // Transport-agnostic current-principal accessor. Singleton: the per-flow value lives in an
            // AsyncLocal, not the instance (mirrors IHttpContextAccessor). Falls back to the ambient HTTP
            // request when nothing is pushed, so HTTP apps work without extra wiring; background-job filters
            // and tests push an explicit principal. TryAdd lets a consumer override with a custom accessor.
            services.TryAddSingleton<ISpiderlyPrincipalAccessor, SpiderlyPrincipalAccessor>();

            services.AddExceptionHandler<SpiderlyExceptionHandler>();
            services.AddProblemDetails(); // Required alongside AddExceptionHandler<T> so UseExceptionHandler() passes its options check
            services.AddMemoryCache();
            services.AddHttpContextAccessor();
            services.AddHttpClient();
            services.AddCors();
            services.SpiderlyConfigureCulture(builder); // Must be before AddControllers
            services.SpiderlyAddControllers();
            services.SpiderlyAddDbContext<TDbContext>(builder.DbProvider, settings.ConnectionString);

            // Optional modules
            if (builder.AuthenticationEnabled)
            {
                services.SpiderlyAddAuthentication();
                services.AddAuthorization();

                // Permission-as-policy: this dynamic provider materializes a `perm:<Code>` policy on demand, so an
                // endpoint can declare [Authorize(SpiderlyAuthorizationPolicies.ForPermission(code))] without
                // pre-registering each policy. Falls through to the default provider for every other policy name.
                // The matching PermissionAuthorizationHandler (which delegates to the consumer's AuthorizationService
                // so its override/API-key cap applies) is wired alongside the security services via
                // AddSpiderlyAuthorization<TAuthorizationService>() — it lives in Spiderly.Security, which this
                // assembly cannot reference, so it cannot be registered here.
                services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

                // Fail loud at boot if the consumer enabled [AuthGuard(...)] (the provider above) but never registered
                // the satisfying handler (forgot AddSpiderlyAuthorization). Without this guard the requirement has no
                // handler, can never Succeed(), and every permission-gated endpoint silently 403s. The guard inspects
                // the final service collection at startup (after the consumer's post-AddSpiderly registrations run).
                services.AddSingleton<IStartupFilter>(new PermissionHandlerRegistrationGuard(services));

                // CSRF protection is global and opt-out (UseSpiderlyCsrf + [IgnoreCsrf]), not an endpoint
                // attribute — per ASP.NET's own antiforgery guidance, per-endpoint opt-in leaves endpoints
                // unprotected by mistake. It used to ride inside [AuthGuard], which had exactly that shape.
                // The guard fails boot if the consumer never adds the middleware.
                services.AddSingleton<CsrfRegistrationGuard>();
                services.AddSingleton<IStartupFilter>(sp => sp.GetRequiredService<CsrfRegistrationGuard>());

                // Resolves a provider code to its id-token validator. Built eagerly at first resolution from
                // the configured providers (+ any consumer-registered custom IExternalAuthProvider), so the
                // generic OIDC validator works config-only. Singleton: holds the per-provider discovery/JWKS caches.
                services.AddSingleton<IExternalAuthProviderRegistry, ExternalAuthProviderRegistry>();

                // Server-side OAuth code-flow helper (Option B2): discovery + authorize-URL + code→token exchange.
                // Singleton: caches a ConfigurationManager per authority.
                services.AddSingleton<ExternalAuthCodeFlow>();
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
                    services.SpiderlyAddBrevoHttpClient(email);
                }
            }

            if (builder.SwaggerEnabled)
            {
                services.SpiderlyAddSwaggerGen();
            }

            if (builder.RateLimitingEnabled)
            {
                services.SpiderlyAddRateLimiters(settings);
            }

            if (builder.ForwardedHeadersEnabled)
            {
                services.SpiderlyAddForwardedHeaders(settings);
            }

            if (builder.OutboxEnabled)
            {
                // Generic over the consumer's concrete outbox entity, so framework code can stage and
                // sweep rows without a compile-time reference to the consumer assembly. Open generics are
                // closed here with the runtime type captured by AddOutbox<TOutbox>().
                Type outboxType = builder.OutboxEntityType;
                services.AddScoped(typeof(IOutbox), typeof(Outbox<>).MakeGenericType(outboxType));
                services.AddScoped(typeof(OutboxDispatcherJob<>).MakeGenericType(outboxType));
            }

            if (builder.NotificationsEnabled)
            {
                // Immutable config + the code→type registry are singletons (the registry is built eagerly here so
                // a duplicate [OutboxCode] fails at boot). Everything else is scoped — channels may depend on
                // scoped/transient services (e.g. EmailChannel → IEmailingService), so nothing here is a singleton
                // holding a channel.
                services.AddSingleton(builder.NotificationRoutingMap);

                // The route keys ARE the complete deliverable set (an unrouted notification is dropped before serialize),
                // so the shared delivery-side registry is built directly from them — no assembly scanning. Eager, so a
                // duplicate [OutboxCode] fails loud at boot.
                services.AddSingleton(new CodeTypeRegistry<INotification>(builder.NotificationRoutingMap.Routes.Keys));
                services.AddScoped<INotificationRouter, DefaultNotificationRouter>();
                services.AddScoped<NotificationDeliveryExecutor>();
                services.AddScoped<NotificationDeliveryJob>();
                services.AddScoped<INotifier, Notifier>();
                services.AddScoped<IOutboxHandler, NotificationOutboxHandler>();

                // Email is the one built-in channel; it needs the emailing service.
                if (builder.EmailingEnabled)
                    services.AddScoped<INotificationChannel, EmailChannel>();

                // Fail fast at startup if a route points at a channel code with no registered channel (otherwise the
                // notification is dropped silently). Runs after the container is built, so it sees channels registered
                // after AddNotifications.
                services.AddHostedService<NotificationRoutingValidator>();
            }

            if (builder.IntegrationEventsEnabled)
            {
                if (!builder.OutboxEnabled)
                    throw new InvalidOperationException(
                        "AddIntegrationEvents() requires the transactional outbox — call AddOutbox<TOutbox>() as well (integration events ride the outbox).");

                // Delivery-side code->type registry, built from the explicitly-registered event types (no assembly
                // scanning). Eager singleton, so a duplicate [OutboxCode] — or a non-event/uncoded type — fails loud at boot.
                services.AddSingleton(new CodeTypeRegistry<IIntegrationEvent>(builder.IntegrationEventTypes));

                // The one outbox handler that fans a delivered event out to its IIntegrationEventHandlers.
                services.AddScoped<IOutboxHandler, IntegrationEventOutboxHandler>();

                // Explicit publisher for facts with no aggregate write (webhooks / jobs / security events).
                services.AddScoped<IIntegrationEventPublisher, IntegrationEventPublisher>();

                // The harvest interceptor, closed over the consumer's outbox entity and registered as a singleton
                // ISaveChangesInterceptor so SpiderlyAddDbContext wires it into the context. Stamps raised events into
                // outbox rows (reading [OutboxCode] off the type) in the same transaction as the entity write.
                Type interceptorType = typeof(IntegrationEventOutboxInterceptor<>).MakeGenericType(builder.OutboxEntityType);
                services.AddSingleton(typeof(ISaveChangesInterceptor), interceptorType);
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
                .AddJwtBearer();

            // Configure the bearer options from the validated JwtOptions / TokenKeyOptions, so the signing
            // key comes from the same Options instance ValidateOnStart guards — a missing JwtKey surfaces the
            // friendly OptionsValidationException instead of a raw ArgumentNullException from GetBytes(null).
            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>, IOptions<TokenKeyOptions>>((options, jwtOptions, tokenKeyOptions) =>
            {
                JwtOptions jwt = jwtOptions.Value;
                TokenKeyOptions tokenKeys = tokenKeyOptions.Value;

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        PathString path = context.HttpContext.Request.Path;

                        string accessTokenKey = tokenKeys.AccessTokenKey;

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
                    ValidIssuer = jwt.JwtIssuer,
                    ValidAudience = jwt.JwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.JwtKey)),
                    ClockSkew = TimeSpan.FromMinutes(jwt.ClockSkewMinutes),
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

        public static void SpiderlyAddDbContext<TDbContext>(this IServiceCollection services, DbProviderCodes dbProvider, string connectionString) where TDbContext : DbContext, IApplicationDbContext
        {
            services.AddDbContext<IApplicationDbContext, TDbContext>((sp, options) =>
            {
                options.UseLazyLoadingProxies();

                if (dbProvider == DbProviderCodes.SQLServer)
                {
                    options.UseSqlServer(connectionString);
                }
                else if (dbProvider == DbProviderCodes.PostgreSQL)
                {
                    options.UseNpgsql(connectionString);
                }

                // Any framework- or consumer-registered SaveChanges interceptors (e.g. the integration-event
                // harvester). Empty — and a no-op — for apps that register none.
                options.AddInterceptors(sp.GetServices<ISaveChangesInterceptor>());
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

        public static void SpiderlyAddRateLimiters(this IServiceCollection services, Settings settings)
        {
            services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

                options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
                {
                    // Which class this request belongs to (trusted first-party infra → api-key principal →
                    // per-IP) is a pure decision extracted to GlobalRateLimitPartitioner so the branch is
                    // unit-testable. The trusted hook is opt-in (null detector ⇒ unchanged behavior) and
                    // requires UseAuthentication before UseRateLimiter for the api-key branch to see the
                    // validated principal — the init template's middleware order.
                    ITrustedCallerDetector trustedCallerDetector =
                        httpContext.RequestServices.GetService<ITrustedCallerDetector>();
                    GlobalRateLimitPartition partition =
                        GlobalRateLimitPartitioner.Resolve(httpContext, settings, trustedCallerDetector);

                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: partition.Key,
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = partition.PermitLimit,
                            Window = TimeSpan.FromSeconds(partition.WindowSeconds),
                            SegmentsPerWindow = 6,
                        }
                    );
                });

                // Customer apps tune the limits via BlobUploadRequestsLimitNumber/Window in appsettings.
                options.AddPolicy(SpiderlyRateLimitPolicies.BlobUpload, httpContext =>
                    RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: Helper.GetIPAddress(httpContext),
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = settings.BlobUploadRequestsLimitNumber,
                            Window = TimeSpan.FromSeconds(settings.BlobUploadRequestsLimitWindow),
                            SegmentsPerWindow = 6,
                        }));

                options.OnRejected = (context, cancellationToken) =>
                {
                    HttpContext httpContext = context.HttpContext;
                    string ip = Helper.GetIPAddress(httpContext) ?? "unknown";
                    string apiKeyId = Helper.GetAuthenticatedApiKeyId(httpContext);
                    string path = httpContext.Request.Path;
                    string method = httpContext.Request.Method;

                    string policyName = "Global";
                    EnableRateLimitingAttribute rateLimitAttr = httpContext.GetEndpoint()
                        ?.Metadata.GetMetadata<EnableRateLimitingAttribute>();
                    if (rateLimitAttr != null)
                    {
                        policyName = rateLimitAttr.PolicyName;
                    }

                    // Warning-level structured log is the whole signal — the app's log pipeline / error
                    // tracker decides whether rejections alert (a rate-limit storm is telemetry, not mail).
                    ILogger logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>()
                        .CreateLogger("Spiderly.RateLimiting");
                    logger.LogWarning(
                        "Rate limit rejected: Policy={Policy}, IP={IP}, ApiKeyId={ApiKeyId}, Method={Method}, Path={Path}",
                        policyName, ip, apiKeyId, method, path);

                    return ValueTask.CompletedTask;
                };
            });
        }

        public static void SpiderlyAddForwardedHeaders(this IServiceCollection services, Settings settings)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.ForwardLimit = settings.ForwardLimit;

                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();

                // Always trust RFC 1918 private networks + loopback (covers Docker, k8s, local reverse proxies)
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Loopback, 8));
                options.KnownNetworks.Add(new IPNetwork(IPAddress.IPv6Loopback, 128));

                // Add any additional trusted proxy networks (e.g. Cloudflare, AWS CloudFront, etc.)
                foreach (string network in settings.TrustedProxyNetworks)
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
        /// and the API key from the bound Spiderly.Shared settings.
        /// </summary>
        public static void SpiderlyAddBrevoHttpClient(this IServiceCollection services, EmailOptions email)
        {
            services.AddHttpClient("Brevo", client =>
            {
                client.BaseAddress = new Uri("https://api.brevo.com/v3/");
                client.DefaultRequestHeaders.Add("api-key", email.BrevoApiKey);
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

        /// <summary>
        /// Adds <see cref="SpiderlyCsrfMiddleware"/>: state-changing requests authenticated by cookie must carry
        /// the <c>X-CSRF</c> header. Place it <b>after</b> <c>UseRouting()</c> (it reads endpoint metadata for the
        /// <c>[IgnoreCsrf]</c> opt-out) and before <c>UseAuthorization()</c>.
        /// </summary>
        /// <remarks>
        /// Required whenever Spiderly authentication is enabled — <c>CsrfRegistrationGuard</c> fails the boot if
        /// this is missing. See <see cref="SpiderlyCsrfMiddleware"/> for how the defense works and why it depends
        /// on the app's CORS origin allow-list.
        /// </remarks>
        /// <param name="app">The application builder.</param>
        public static void UseSpiderlyCsrf(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetRequiredService<CsrfRegistrationGuard>().MarkRegistered();
            app.UseMiddleware<SpiderlyCsrfMiddleware>();
        }

        public static void SpiderlyConfigureExceptionHandling(this IApplicationBuilder app)
        {
            app.UseExceptionHandler();
        }

        /// <summary>
        /// Schedules the recurring outbox jobs for the consumer's outbox entity <typeparamref name="TOutbox"/>: the
        /// dispatcher sweep, plus daily retention (<c>OutboxRetentionJob</c>) and a 5-minute health check
        /// (<c>OutboxHealthJob</c>). Call in the Configure phase, after Hangfire is initialized. Requires
        /// <c>spiderly.AddOutbox&lt;TOutbox&gt;()</c> during service registration; tune via <c>OutboxOptions</c>.
        /// </summary>
        /// <param name="app">The application builder (Configure phase marker; the schedule is registered globally via Hangfire).</param>
        /// <param name="cronExpression">Sweep cadence. Defaults to once a minute; pass a faster (e.g. seconds-based) cron if your Hangfire is configured for it.</param>
        /// <example>
        /// <code>
        /// app.SpiderlyUseOutboxRecurringJob&lt;OutboxMessage&gt;();
        /// </code>
        /// </example>
        public static void SpiderlyUseOutboxRecurringJob<TOutbox>(this IApplicationBuilder app, string cronExpression = null)
            where TOutbox : class, IOutboxMessage, new()
        {
            RecurringJob.AddOrUpdate<OutboxDispatcherJob<TOutbox>>(
                "outbox-dispatcher",
                job => job.ProcessAsync(),
                cronExpression ?? Cron.Minutely());

            // Storage hygiene + ops visibility, both generic over TOutbox and tuned via OutboxOptions: retention purges
            // handled rows past the window; health logs an error (→ your alerting) on backlog age / dead-letters.
            RecurringJob.AddOrUpdate<OutboxRetentionJob<TOutbox>>(
                "outbox-retention", job => job.PurgeAsync(), Cron.Daily());
            RecurringJob.AddOrUpdate<OutboxHealthJob<TOutbox>>(
                "outbox-health", job => job.CheckAsync(), "*/5 * * * *");
        }

        /// <summary>
        /// Registers the global Hangfire filter that carries the current principal into background jobs: it
        /// captures the enqueuing principal (when one is authenticated) and restores it for the duration of job
        /// execution via <see cref="ISpiderlyPrincipalAccessor"/>, defaulting to
        /// <see cref="Authorization.SpiderlyPrincipal.System"/> for recurring / scheduler-enqueued jobs. Call in
        /// the Configure phase (after the DI container is built). Only the principal id and kind travel with the
        /// job — never tokens; background work carries attribution, not re-authorization.
        /// <example>
        /// <code>
        /// app.SpiderlyUseHangfirePrincipalFilter();
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="app">The application builder (Configure phase); the filter is registered globally via Hangfire.</param>
        public static void SpiderlyUseHangfirePrincipalFilter(this IApplicationBuilder app)
        {
            IServiceProvider services = app.ApplicationServices;
            GlobalJobFilters.Filters.Add(new HangfirePrincipalFilter(services.GetRequiredService<ISpiderlyPrincipalAccessor>()));
        }

        #endregion

    }
}
