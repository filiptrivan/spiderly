namespace Spiderly.Security.Interfaces
{
    public interface ITokenStorage<T> where T : class, IExpirableToken
    {
        Task AddOrUpdateAsync(string key, T token);

        /// <summary>Returns the token stored under <paramref name="key"/>, or <c>null</c> when there is none — "not found" is null by design.</summary>
        Task<T?> TryGetValueAsync(string key);
        Task<bool> TryRemoveAsync(string key);
        Task<IEnumerable<KeyValuePair<string, T>>> GetAllAsync();
        Task<IEnumerable<KeyValuePair<string, T>>> WhereAsync(Func<KeyValuePair<string, T>, bool> predicate);

        /// <summary>
        /// Retrieves all tokens matching the given secondary index value.
        /// Index names must match those configured during storage registration.
        /// <example>
        /// <code>
        /// IEnumerable&lt;KeyValuePair&lt;string, RefreshTokenDTO&gt;&gt; tokens =
        ///     await _storage.GetByIndexAsync(RefreshTokenDTO.UserIdIndex, userId.ToString());
        /// </code>
        /// </example>
        /// </summary>
        Task<IEnumerable<KeyValuePair<string, T>>> GetByIndexAsync(string indexName, string indexValue);
    }
}
