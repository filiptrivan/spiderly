namespace Spiderly.Security.Interfaces
{
    public interface ITokenStorage<T> where T : class, IExpirableToken
    {
        Task AddOrUpdateAsync(string key, T token);
        Task<T> TryGetValueAsync(string key);
        Task<bool> TryRemoveAsync(string key);
        Task<IEnumerable<KeyValuePair<string, T>>> GetAllAsync();
        Task<IEnumerable<KeyValuePair<string, T>>> WhereAsync(Func<KeyValuePair<string, T>, bool> predicate);
    }
}
