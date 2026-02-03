using Spiderly.Security.Interfaces;
using StackExchange.Redis;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    public class RedisTokenStorage<T> : ITokenStorage<T> where T : class, IExpirableToken
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly IDatabase _database;
        private readonly string _keyPrefix;

        public RedisTokenStorage(IConnectionMultiplexer redis, string keyPrefix)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _keyPrefix = keyPrefix;
        }

        public async Task AddOrUpdateAsync(string key, T token)
        {
            string redisKey = _keyPrefix + key;
            string serializedToken = JsonSerializer.Serialize(token);
            TimeSpan? expiration = token.ExpiresAt > DateTime.UtcNow ? token.ExpiresAt - DateTime.UtcNow : null;

            await _database.StringSetAsync(redisKey, serializedToken, expiration);
        }

        public async Task<T> TryGetValueAsync(string key)
        {
            string redisKey = _keyPrefix + key;
            RedisValue value = await _database.StringGetAsync(redisKey);

            if (value.IsNullOrEmpty)
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(value);
        }

        public async Task<bool> TryRemoveAsync(string key)
        {
            T token = await TryGetValueAsync(key);

            if (token != null)
            {
                string redisKey = _keyPrefix + key;
                await _database.KeyDeleteAsync(redisKey);
                return true;
            }

            return false;
        }

        public async Task<IEnumerable<KeyValuePair<string, T>>> GetAllAsync()
        {
            List<KeyValuePair<string, T>> result = new();
            IServer server = _redis.GetServer(_redis.GetEndPoints().First());

            await foreach (RedisKey key in server.KeysAsync(pattern: _keyPrefix + "*"))
            {
                RedisValue value = await _database.StringGetAsync(key);
                if (!value.IsNullOrEmpty)
                {
                    T token = JsonSerializer.Deserialize<T>(value);
                    string tokenKey = key.ToString().Replace(_keyPrefix, "");
                    result.Add(new KeyValuePair<string, T>(tokenKey, token));
                }
            }

            return result;
        }

        public async Task<IEnumerable<KeyValuePair<string, T>>> WhereAsync(Func<KeyValuePair<string, T>, bool> predicate)
        {
            IEnumerable<KeyValuePair<string, T>> allTokens = await GetAllAsync();
            return allTokens.Where(predicate).ToList();
        }
    }
}
