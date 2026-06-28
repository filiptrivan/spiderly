using System;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Security.Authentication;
using Spiderly.Security.Interfaces;

namespace Spiderly.Security.Extensions
{
    /// <summary>
    /// Configures the genuinely-optional security add-ons inside
    /// <see cref="SecurityRegistrationExtensions.AddSecurity{TUser, TUserExternalLogin, TAuthorizationService}"/>.
    /// The mandatory auth core (human login + permission authorization) is always registered by <c>AddSecurity</c>;
    /// the pieces here are opt-in, so an app that doesn't use them never has to name their types.
    /// </summary>
    public sealed class SpiderlySecurityBuilder
    {
        /// <summary>The underlying service collection, for advanced/custom registrations.</summary>
        public IServiceCollection Services { get; }

        internal SpiderlySecurityBuilder(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// Enables API-key authentication: registers <typeparamref name="TApiKey"/> as the <c>ApiKey</c> principal
        /// kind and installs the API-key scheme so existing JWT-protected endpoints also accept an <c>X-Api-Key</c>
        /// header (see <see cref="ApiKeyAuthenticationRegistrationExtensions.AddSpiderlyApiKeyAuthentication{TApiKey}"/>).
        /// Optional — call only if the app exposes API-key access.
        /// </summary>
        /// <typeparam name="TApiKey">The application's API-key entity.</typeparam>
        /// <param name="configure">Optional hook to customize the API-key scheme options (e.g. the header name).</param>
        public SpiderlySecurityBuilder AddApiKeys<TApiKey>(Action<ApiKeyAuthenticationOptions> configure = null)
            where TApiKey : class, IApiKey, new()
        {
            Services.AddSpiderlyPrincipal<TApiKey>(PrincipalKinds.ApiKey);
            Services.AddSpiderlyApiKeyAuthentication<TApiKey>(configure);
            return this;
        }
    }
}
