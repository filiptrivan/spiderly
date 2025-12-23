using Spiderly.Security.Interfaces;
using System.Collections.Concurrent;

namespace Spiderly.Security.Services
{
    public class InMemoryTokenStorage<T> : ITokenStorage<T> where T : class
    {
        // Making ConcurrentDictionary if two users are searching for the refresh token in the same time
        // The maximum number of the refresh tokens inside dictionary is SettingsProvider.Current.AllowedBrowsersForTheSingleUser
        private readonly ConcurrentDictionary<string, T> _storage = new();

        public Task AddOrUpdateAsync(string key, T token)
        {
            _storage.AddOrUpdate(key, token, (_, _) => token);
            return Task.CompletedTask;
        }

        public Task<T> TryGetValueAsync(string key)
        {
            _storage.TryGetValue(key, out T token);
            return Task.FromResult(token);
        }

        public Task<bool> TryRemoveAsync(string key)
        {
            bool removed = _storage.TryRemove(key, out _);
            return Task.FromResult(removed);
        }

        public Task<IEnumerable<KeyValuePair<string, T>>> GetAllAsync()
        {
            IEnumerable<KeyValuePair<string, T>> result = _storage.ToList();
            return Task.FromResult(result);
        }

        public Task<IEnumerable<KeyValuePair<string, T>>> WhereAsync(Func<KeyValuePair<string, T>, bool> predicate)
        {
            IEnumerable<KeyValuePair<string, T>> result = _storage.Where(predicate).ToList();
            return Task.FromResult(result);
        }
    }
}
