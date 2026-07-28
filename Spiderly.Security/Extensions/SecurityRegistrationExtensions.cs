using System;
using Spiderly.Shared.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Extensions;

namespace Spiderly.Security.Extensions
{
    /// <summary>
    /// One-call registration of the co-required Spiderly security core. A working auth setup is all-or-nothing:
    /// the current-principal/login service, the JWT manager, the human principal kind, and the
    /// <c>[AuthGuard(...)]</c> authorization handler + its <c>AuthorizationServiceBase</c> forwarding are useless
    /// apart and silently break the app when any is missing (a forgotten handler 403s every permission-gated
    /// endpoint). Bundling them here makes that drift impossible; the genuinely-optional pieces (API keys) opt in
    /// through the <see cref="SpiderlySecurityBuilder"/> sub-builder.
    /// <para>Lives in <c>Spiderly.Security</c> (so it can name the security service types) as an extension on the
    /// <c>Spiderly.Shared</c> <see cref="SpiderlyBuilder"/>, so it reads as one fluent step inside <c>AddSpiderly</c>
    /// despite the one-way assembly dependency.</para>
    /// </summary>
    public static class SecurityRegistrationExtensions
    {
        /// <summary>
        /// Registers the mandatory authentication + authorization core and enables it (so a separate
        /// <c>spiderly.AddAuthentication()</c> is not needed). Call inside the <c>AddSpiderly</c> builder lambda.
        /// <example>
        /// <code>
        /// services.AddSpiderly&lt;MyDbContext&gt;(config, spiderly =>
        /// {
        ///     spiderly.UsePostgreSQL();
        ///     spiderly.AddSecurity&lt;User, UserExternalLogin, AuthorizationService&gt;(s => s.AddApiKeys&lt;ApiKey&gt;());
        ///     spiderly.AddTokenStorage();
        /// });
        /// </code>
        /// </example>
        /// </summary>
        /// <typeparam name="TUser">The application's human-user entity (the email-login principal).</typeparam>
        /// <typeparam name="TUserExternalLogin">The entity linking a user to an external-provider login.</typeparam>
        /// <typeparam name="TAuthorizationService">
        /// The application's authorization service — its generated <c>AuthorizationServiceGenerated</c> or a
        /// hand-written subclass. <c>AuthorizationServiceBase</c> is forwarded to it so <c>[AuthGuard(...)]</c> honors
        /// its overrides (e.g. an API-key role cap).
        /// </typeparam>
        /// <param name="builder">The Spiderly builder (the lambda parameter of <c>AddSpiderly</c>).</param>
        /// <param name="configure">Optional add-ons (e.g. <c>s => s.AddApiKeys&lt;ApiKey&gt;()</c>).</param>
        public static SpiderlyBuilder AddSecurity<TUser, TUserExternalLogin, TAuthorizationService>(
            this SpiderlyBuilder builder,
            Action<SpiderlySecurityBuilder> configure = null)
            where TUser : class, IUser, new()
            where TUserExternalLogin : class, IUserExternalLogin, new()
            where TAuthorizationService : AuthorizationServiceBase
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            IServiceCollection services = builder.Services;

            // Current-principal + email-login + token services. TryAdd so a consumer can pre-register a custom one.
            services.TryAddTransient<AuthenticationService>();
            services.TryAddTransient<SecurityServiceBase<TUser, TUserExternalLogin>>();
            services.TryAddSingleton<IJwtAuthManager, JwtAuthManagerService>();

            // The human principal kind: kind-dispatched authorization resolves it by the principal_kind claim.
            services.AddSpiderlyPrincipal<TUser>(PrincipalKinds.User, PrincipalNature.Human);

            // Authorization service + the [AuthGuard(...)] handler that forwards to it. This is the pairing that,
            // when split across the registration surface and forgotten, silently 403s every permission-gated endpoint.
            services.TryAddTransient<TAuthorizationService>();
            services.AddSpiderlyAuthorization<TAuthorizationService>();

            // Turn on the Shared auth infrastructure (JWT scheme, permission policy provider, external auth, and the
            // PermissionHandlerRegistrationGuard) so AddSecurity is the single call — not AddSecurity + AddAuthentication.
            builder.AddAuthentication();

            configure?.Invoke(new SpiderlySecurityBuilder(services));

            return builder;
        }
    }
}
