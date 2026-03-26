using Microsoft.Extensions.DependencyInjection;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
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
        /// services.AddTokenStorage();
        /// </code>
        /// </example>
        /// </summary>
        public static IServiceCollection AddTokenStorage(this IServiceCollection services)
        {
            if (SettingsProvider.Current.UseRedisCache)
            {
                services.AddSingleton<IConnectionMultiplexer>(sp =>
                    ConnectionMultiplexer.Connect(SettingsProvider.Current.RedisConnectionString));

                services.AddSingleton<ITokenStorage<RefreshTokenDTO>>(sp =>
                    new RedisTokenStorage<RefreshTokenDTO>(sp.GetRequiredService<IConnectionMultiplexer>(), "refresh:"));

                services.AddSingleton<ITokenStorage<LoginVerificationTokenDTO>>(sp =>
                    new RedisTokenStorage<LoginVerificationTokenDTO>(sp.GetRequiredService<IConnectionMultiplexer>(), "login_verification:"));
            }
            else
            {
                services.AddSingleton<ITokenStorage<RefreshTokenDTO>, InMemoryTokenStorage<RefreshTokenDTO>>();
                services.AddSingleton<ITokenStorage<LoginVerificationTokenDTO>, InMemoryTokenStorage<LoginVerificationTokenDTO>>();
            }

            return services;
        }
    }
}
