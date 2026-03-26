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
        private readonly Dictionary<string, Func<T, string>> _indexExtractors;

        public RedisTokenStorage(IConnectionMultiplexer redis, string keyPrefix, Dictionary<string, Func<T, string>> indexExtractors = null)
        {
            _redis = redis;
            _database = redis.GetDatabase();
            _keyPrefix = keyPrefix;
            _indexExtractors = indexExtractors ?? new Dictionary<string, Func<T, string>>();
        }

        public async Task AddOrUpdateAsync(string key, T token)
        {
            string redisKey = _keyPrefix + key;
            TimeSpan? expiration = token.ExpiresAt > DateTime.UtcNow ? token.ExpiresAt - DateTime.UtcNow : null;

            // Old index entries must be removed before writing, otherwise stale set references accumulate if the indexed property changed
            if (_indexExtractors.Count > 0)
            {
                T existingToken = await TryGetValueAsync(key);
                if (existingToken != null)
                {
                    await RemoveFromIndexesAsync(key, existingToken);
                }
            }

            string serializedToken = JsonSerializer.Serialize(token);
            await _database.StringSetAsync(redisKey, serializedToken, expiration);

            await AddToIndexesAsync(key, token, expiration);
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
            // Must read before delete — once the key is gone, we can no longer derive which index sets to clean up
            if (_indexExtractors.Count > 0)
            {
                T token = await TryGetValueAsync(key);
                if (token != null)
                {
                    await RemoveFromIndexesAsync(key, token);
                }
            }

            string redisKey = _keyPrefix + key;
            return await _database.KeyDeleteAsync(redisKey);
        }

        public async Task<IEnumerable<KeyValuePair<string, T>>> GetAllAsync()
        {
            IServer server = _redis.GetServer(_redis.GetEndPoints().First());
            List<RedisKey> keys = new();

            await foreach (RedisKey key in server.KeysAsync(pattern: _keyPrefix + "*"))
            {
                keys.Add(key);
            }

            if (keys.Count == 0)
                return Enumerable.Empty<KeyValuePair<string, T>>();

            RedisValue[] values = await _database.StringGetAsync(keys.ToArray());

            List<KeyValuePair<string, T>> result = new();
            for (int i = 0; i < keys.Count; i++)
            {
                if (!values[i].IsNullOrEmpty)
                {
                    T token = JsonSerializer.Deserialize<T>(values[i]);
                    string tokenKey = keys[i].ToString().Substring(_keyPrefix.Length);
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

        /// <summary>
        /// Retrieves all tokens matching the given secondary index value.
        /// Uses Redis Set lookup (SMEMBERS) followed by batched MGET.
        /// Performs lazy cleanup of stale entries (expired tokens still referenced in the index Set).
        /// </summary>
        public async Task<IEnumerable<KeyValuePair<string, T>>> GetByIndexAsync(string indexName, string indexValue)
        {
            if (!_indexExtractors.ContainsKey(indexName))
                throw new ArgumentException($"Index '{indexName}' is not configured for this token storage.", nameof(indexName));

            string indexKey = GetIndexKey(indexName, indexValue);
            RedisValue[] members = await _database.SetMembersAsync(indexKey);

            if (members.Length == 0)
                return Enumerable.Empty<KeyValuePair<string, T>>();

            RedisKey[] redisKeys = members.Select(m => (RedisKey)(_keyPrefix + m.ToString())).ToArray();
            RedisValue[] values = await _database.StringGetAsync(redisKeys);

            List<KeyValuePair<string, T>> result = new();
            List<RedisValue> staleMembers = new();

            for (int i = 0; i < members.Length; i++)
            {
                if (values[i].IsNullOrEmpty)
                {
                    staleMembers.Add(members[i]);
                }
                else
                {
                    T token = JsonSerializer.Deserialize<T>(values[i]);
                    result.Add(new KeyValuePair<string, T>(members[i].ToString(), token));
                }
            }

            // Lazy cleanup: remove stale references from the index Set (fire-and-forget)
            if (staleMembers.Count > 0)
            {
                _ = _database.SetRemoveAsync(indexKey, staleMembers.ToArray(), CommandFlags.FireAndForget);
            }

            return result;
        }

        private string GetIndexKey(string indexName, string indexValue)
        {
            return $"idx:{_keyPrefix}{indexName}:{indexValue}";
        }

        private async Task AddToIndexesAsync(string key, T token, TimeSpan? expiration)
        {
            foreach (KeyValuePair<string, Func<T, string>> extractor in _indexExtractors)
            {
                string indexValue = extractor.Value(token);
                if (indexValue == null)
                    continue;

                string indexKey = GetIndexKey(extractor.Key, indexValue);
                await _database.SetAddAsync(indexKey, key);

                // Set TTL on the index Set with a buffer so it outlives all member tokens
                if (expiration.HasValue)
                {
                    TimeSpan indexExpiration = expiration.Value.Add(TimeSpan.FromMinutes(5));
                    await _database.KeyExpireAsync(indexKey, indexExpiration, ExpireWhen.GreaterThanCurrentExpiry);
                }
            }
        }

        private async Task RemoveFromIndexesAsync(string key, T token)
        {
            foreach (KeyValuePair<string, Func<T, string>> extractor in _indexExtractors)
            {
                string indexValue = extractor.Value(token);
                if (indexValue == null)
                    continue;

                string indexKey = GetIndexKey(extractor.Key, indexValue);
                await _database.SetRemoveAsync(indexKey, key);
            }
        }
    }
}
