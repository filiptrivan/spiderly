using Microsoft.Extensions.Caching.Distributed;
using Spiderly.Security.DTO;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    public class RedisTokenCacheService : ITokenCacheService
    {
        private readonly IDistributedCache _cache;
        private const string RefreshTokenPrefix = "refresh_token";
        private const string LoginVerificationTokenPrefix = "login_verification";

        public RedisTokenCacheService(IDistributedCache cache)
        {
            _cache = cache;
        }

        public async Task<RefreshTokenDTO?> GetRefreshTokenAsync(string userId, string browserId)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            string? json = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<RefreshTokenDTO>(json);
        }

        public async Task SetRefreshTokenAsync(string userId, string browserId, RefreshTokenDTO token, TimeSpan expiration)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            string json = JsonSerializer.Serialize(token);

            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _cache.SetStringAsync(key, json, options);
        }

        public async Task<bool> RemoveRefreshTokenAsync(string userId, string browserId)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            await _cache.RemoveAsync(key);
            return true;
        }

        public async Task<IEnumerable<RefreshTokenDTO>> GetAllRefreshTokensForUserAsync(string userId)
        {
            List<RefreshTokenDTO> tokens = new List<RefreshTokenDTO>();
            string pattern = $"{RefreshTokenPrefix}:{userId}:*";

            if (_cache is Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache redisCache)
            {
                StackExchange.Redis.IDatabase db = redisCache.GetDatabase();
                StackExchange.Redis.IServer server = redisCache.GetServer();

                foreach (StackExchange.Redis.RedisKey key in server.Keys(pattern: pattern))
                {
                    string? json = await db.StringGetAsync(key);
                    if (!string.IsNullOrEmpty(json))
                    {
                        RefreshTokenDTO? token = JsonSerializer.Deserialize<RefreshTokenDTO>(json);
                        if (token != null)
                            tokens.Add(token);
                    }
                }
            }

            return tokens;
        }

        public async Task RemoveAllRefreshTokensForUserAsync(string userId)
        {
            if (_cache is Microsoft.Extensions.Caching.StackExchangeRedis.RedisCache redisCache)
            {
                StackExchange.Redis.IDatabase db = redisCache.GetDatabase();
                StackExchange.Redis.IServer server = redisCache.GetServer();
                string pattern = $"{RefreshTokenPrefix}:{userId}:*";

                foreach (StackExchange.Redis.RedisKey key in server.Keys(pattern: pattern))
                {
                    await db.KeyDeleteAsync(key);
                }
            }
        }

        public async Task<LoginVerificationTokenDTO?> GetLoginVerificationTokenAsync(string email, string browserId)
        {
            string key = GetLoginVerificationKey(email, browserId);
            string? json = await _cache.GetStringAsync(key);

            if (string.IsNullOrEmpty(json))
                return null;

            return JsonSerializer.Deserialize<LoginVerificationTokenDTO>(json);
        }

        public async Task SetLoginVerificationTokenAsync(string email, string browserId, LoginVerificationTokenDTO token, TimeSpan expiration)
        {
            string key = GetLoginVerificationKey(email, browserId);
            string json = JsonSerializer.Serialize(token);

            DistributedCacheEntryOptions options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiration
            };

            await _cache.SetStringAsync(key, json, options);
        }

        public async Task<bool> RemoveLoginVerificationTokenAsync(string email, string browserId)
        {
            string key = GetLoginVerificationKey(email, browserId);
            await _cache.RemoveAsync(key);
            return true;
        }

        private static string GetRefreshTokenKey(string userId, string browserId)
        {
            return $"{RefreshTokenPrefix}:{userId}:{browserId}";
        }

        private static string GetLoginVerificationKey(string email, string browserId)
        {
            return $"{LoginVerificationTokenPrefix}:{email}:{browserId}";
        }
    }
}
