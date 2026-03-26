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
            builder.Services.AddSpiderlyTokenStorage();
            return builder;
        }

        /// <summary>
        /// Registers token storage for refresh tokens and login verification tokens.
        /// Uses Redis when <see cref="Settings.UseRedisCache"/> is enabled, otherwise uses in-memory storage.
        /// Prefer using the <see cref="AddTokenStorage(SpiderlyBuilder)"/> builder extension instead.
        /// </summary>
        public static IServiceCollection AddSpiderlyTokenStorage(this IServiceCollection services)
        {
            Dictionary<string, Func<RefreshTokenDTO, string>> refreshTokenIndexes = new()
            {
                [RefreshTokenDTO.UserIdIndex] = token => token.UserId.ToString(),
            };

            Dictionary<string, Func<LoginVerificationTokenDTO, string>> loginVerificationIndexes = new()
            {
                [LoginVerificationTokenDTO.EmailIndex] = token => token.Email,
            };

            if (SettingsProvider.Current.UseRedisCache)
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(SettingsProvider.Current.RedisConnectionString));

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
