using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Extensions;
using StackExchange.Redis;

namespace Spiderly.Security.Extensions
{
    public static class TokenStorageExtensions
    {
        /// <summary>
        /// Registers token storage for refresh tokens and login verification tokens.
        /// Uses Redis when <see cref="Settings.UseRedisCache"/> is enabled, otherwise uses in-memory storage.
        /// <example>
        /// <code>
        /// services.AddSpiderly&lt;MyDbContext&gt;(spiderly =&gt;
        /// {
        ///     spiderly.AddAuthentication();
        ///     spiderly.AddTokenStorage();
        /// });
        /// </code>
        /// </example>
        /// </summary>
        public static SpiderlyBuilder AddTokenStorage(this SpiderlyBuilder builder)
        {
            builder.Services.AddSpiderlyTokenStorage(builder.Configuration);
            return builder;
        }

        /// <summary>
        /// Registers token storage for refresh tokens and login verification tokens.
        /// Uses Redis when <see cref="Settings.UseRedisCache"/> is enabled, otherwise uses in-memory storage.
        /// Prefer using the <see cref="AddTokenStorage(SpiderlyBuilder)"/> builder extension instead.
        /// </summary>
        public static IServiceCollection AddSpiderlyTokenStorage(this IServiceCollection services, IConfiguration configuration)
        {
            // Bind AuthPolicyOptions from the Spiderly.Security section via the Options pattern, so the
            // security services inject IOptions<AuthPolicyOptions> and validation runs at startup. (Token
            // cookie key names — TokenKeyOptions — live in Spiderly.Shared as the single source of truth.)
            services.AddOptions<AuthPolicyOptions>()
                .Bind(configuration.GetSection(Settings.ConfigurationSection))
                .ValidateOnStart();

            // Redis is selected at composition time but CONNECTED lazily, so a missing connection string
            // would otherwise surface as a 500 on the first login rather than at boot. Validate the value
            // only — not reachability: eagerly connecting would make the app refuse to start whenever Redis
            // is momentarily down, and StackExchange.Redis reconnects on its own.
            services.AddOptions<Settings>()
                .Bind(configuration.GetSection(Settings.ConfigurationSection))
                .Validate(
                    o => !o.UseRedisCache || !string.IsNullOrWhiteSpace(o.RedisConnectionString),
                    $"Spiderly: '{Settings.ConfigurationSection}:RedisConnectionString' is required when UseRedisCache is enabled.")
                .ValidateOnStart();

            // The token-storage backend selection is read once here at composition time (not injected).
            Settings securitySettings = configuration.GetSection(Settings.ConfigurationSection).Get<Settings>() ?? new();

            Dictionary<string, Func<RefreshTokenDTO, string?>> refreshTokenIndexes = new()
            {
                [RefreshTokenDTO.UserIdIndex] = token => token.UserId.ToString(),
            };

            Dictionary<string, Func<LoginVerificationTokenDTO, string?>> loginVerificationIndexes = new()
            {
                [LoginVerificationTokenDTO.EmailIndex] = token => token.Email,
            };

            if (securitySettings.UseRedisCache)
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    // !: the ValidateOnStart guard above fails the boot when this is null or empty.
                    ConnectionMultiplexer.Connect(securitySettings.RedisConnectionString!));

                services.AddSingleton<ITokenStorage<RefreshTokenDTO>>(sp =>
                    new RedisTokenStorage<RefreshTokenDTO>(
                        sp.GetRequiredService<IConnectionMultiplexer>(),
                        "refresh:",
                        refreshTokenIndexes));

                services.AddSingleton<ITokenStorage<LoginVerificationTokenDTO>>(sp =>
                    new RedisTokenStorage<LoginVerificationTokenDTO>(
                        sp.GetRequiredService<IConnectionMultiplexer>(),
                        "login_verification:",
                        loginVerificationIndexes));
            }
            else
            {
                services.AddSingleton<ITokenStorage<RefreshTokenDTO>>(sp =>
                    new InMemoryTokenStorage<RefreshTokenDTO>(refreshTokenIndexes));

                services.AddSingleton<ITokenStorage<LoginVerificationTokenDTO>>(sp =>
                    new InMemoryTokenStorage<LoginVerificationTokenDTO>(loginVerificationIndexes));
            }

            return services;
        }
    }
}
