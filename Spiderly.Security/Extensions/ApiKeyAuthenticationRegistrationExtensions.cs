using System;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiderly.Security.Authentication;
using Spiderly.Security.Interfaces;

namespace Spiderly.Security.Extensions
{
    /// <summary>
    /// Registers API-key authentication: the <c>ApiKey</c> scheme plus a forwarding policy scheme that lets
    /// existing JWT-protected endpoints also accept an <c>X-Api-Key</c> header — with no per-endpoint change.
    /// Call after <c>AddSpiderly(...)</c>, alongside the API-key principal registration.
    /// <example>
    /// <code>
    /// services.AddSpiderlyPrincipal&lt;ApiKey&gt;(PrincipalKinds.ApiKey);
    /// services.AddSpiderlyApiKeyAuthentication&lt;ApiKey&gt;();
    /// </code>
    /// </example>
    /// </summary>
    public static class ApiKeyAuthenticationRegistrationExtensions
    {
        /// <summary>
        /// Registers the default <see cref="DefaultApiKeyAuthenticator{TApiKey}"/> over the application's
        /// <typeparamref name="TApiKey"/> entity, adds the
        /// <see cref="ApiKeyAuthenticationDefaults.AuthenticationScheme"/> handler, and installs a forwarding
        /// policy scheme (<see cref="ApiKeyAuthenticationDefaults.PolicyScheme"/>) as the default
        /// authenticate/challenge scheme. The selector forwards to the API-key scheme when the configured
        /// header is present and to JWT bearer otherwise, so either credential authenticates an endpoint.
        /// To use a custom lookup, register your own <see cref="IApiKeyAuthenticator"/> before calling this —
        /// the default is added with <c>TryAdd</c>, so your registration wins.
        /// </summary>
        /// <typeparam name="TApiKey">The application's API-key entity (implements <see cref="IApiKey"/>).</typeparam>
        /// <param name="services">The DI service collection, after <c>AddSpiderly</c> has registered JWT bearer.</param>
        /// <param name="configureOptions">Optional hook to customize the API-key scheme options (e.g. the header name).</param>
        /// <returns>The same <paramref name="services"/> for chaining.</returns>
        public static IServiceCollection AddSpiderlyApiKeyAuthentication<TApiKey>(
            this IServiceCollection services,
            Action<ApiKeyAuthenticationOptions> configureOptions = null)
            where TApiKey : class, IApiKey
        {
            // Default key lookup over the app's TApiKey table; a consumer can register its own
            // IApiKeyAuthenticator before this call to override (TryAdd keeps the existing registration).
            // Scoped: the authenticator queries the (scoped) application DbContext per request.
            services.TryAddScoped<IApiKeyAuthenticator, DefaultApiKeyAuthenticator<TApiKey>>();

            // Resolve the header name once at registration (honoring any configureOptions override) so the
            // per-request forwarding selector below closes over a constant instead of resolving options each call.
            ApiKeyAuthenticationOptions schemeOptions = new();
            configureOptions?.Invoke(schemeOptions);
            string headerName = schemeOptions.HeaderName;

            // Add the ApiKey scheme and a forwarding policy scheme to the existing authentication setup.
            // The no-arg AddAuthentication() does not change the defaults AddSpiderly configured for JWT bearer.
            services.AddAuthentication()
                .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                    ApiKeyAuthenticationDefaults.AuthenticationScheme, configureOptions)
                .AddPolicyScheme(ApiKeyAuthenticationDefaults.PolicyScheme, ApiKeyAuthenticationDefaults.PolicyScheme, options =>
                {
                    // Forward authenticate/challenge/forbid to the API-key scheme when its header is present,
                    // otherwise to JWT bearer.
                    options.ForwardDefaultSelector = context =>
                        context.Request.Headers.ContainsKey(headerName)
                            ? ApiKeyAuthenticationDefaults.AuthenticationScheme
                            : JwtBearerDefaults.AuthenticationScheme;
                });

            // Route UseAuthentication() through the forwarding scheme so a [HasPermission]/[Authorize] endpoint
            // accepts either credential. Post-configures the defaults set by AddSpiderly's JWT registration;
            // this runs only when API-key auth is opted into.
            services.Configure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = ApiKeyAuthenticationDefaults.PolicyScheme;
                options.DefaultAuthenticateScheme = ApiKeyAuthenticationDefaults.PolicyScheme;
                options.DefaultChallengeScheme = ApiKeyAuthenticationDefaults.PolicyScheme;
            });

            return services;
        }
    }
}
