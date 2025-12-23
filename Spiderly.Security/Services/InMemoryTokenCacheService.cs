using Spiderly.Security.DTO;
using System.Collections.Concurrent;

namespace Spiderly.Security.Services
{
    public class InMemoryTokenCacheService : ITokenCacheService
    {
        private readonly ConcurrentDictionary<string, RefreshTokenDTO> _refreshTokens = new();
        private readonly ConcurrentDictionary<string, LoginVerificationTokenDTO> _loginVerificationTokens = new();

        public Task<RefreshTokenDTO?> GetRefreshTokenAsync(string userId, string browserId)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            _refreshTokens.TryGetValue(key, out RefreshTokenDTO? token);

            if (token != null && token.ExpireAt < DateTime.UtcNow)
            {
                _refreshTokens.TryRemove(key, out _);
                return Task.FromResult<RefreshTokenDTO?>(null);
            }

            return Task.FromResult(token);
        }

        public Task SetRefreshTokenAsync(string userId, string browserId, RefreshTokenDTO token, TimeSpan expiration)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            _refreshTokens[key] = token;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveRefreshTokenAsync(string userId, string browserId)
        {
            string key = GetRefreshTokenKey(userId, browserId);
            bool removed = _refreshTokens.TryRemove(key, out _);
            return Task.FromResult(removed);
        }

        public Task<IEnumerable<RefreshTokenDTO>> GetAllRefreshTokensForUserAsync(string userId)
        {
            IEnumerable<RefreshTokenDTO> tokens = _refreshTokens
                .Where(kvp => kvp.Value.UserId.ToString() == userId)
                .Where(kvp => kvp.Value.ExpireAt >= DateTime.UtcNow)
                .Select(kvp => kvp.Value);

            return Task.FromResult(tokens);
        }

        public Task RemoveAllRefreshTokensForUserAsync(string userId)
        {
            List<string> keysToRemove = _refreshTokens
                .Where(kvp => kvp.Value.UserId.ToString() == userId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (string key in keysToRemove)
            {
                _refreshTokens.TryRemove(key, out _);
            }

            return Task.CompletedTask;
        }

        public Task<LoginVerificationTokenDTO?> GetLoginVerificationTokenAsync(string email, string browserId)
        {
            string key = GetLoginVerificationKey(email, browserId);
            _loginVerificationTokens.TryGetValue(key, out LoginVerificationTokenDTO? token);

            if (token != null && token.ExpireAt < DateTime.UtcNow)
            {
                _loginVerificationTokens.TryRemove(key, out _);
                return Task.FromResult<LoginVerificationTokenDTO?>(null);
            }

            return Task.FromResult(token);
        }

        public Task SetLoginVerificationTokenAsync(string email, string browserId, LoginVerificationTokenDTO token, TimeSpan expiration)
        {
            string key = GetLoginVerificationKey(email, browserId);
            _loginVerificationTokens[key] = token;
            return Task.CompletedTask;
        }

        public Task<bool> RemoveLoginVerificationTokenAsync(string email, string browserId)
        {
            string key = GetLoginVerificationKey(email, browserId);
            bool removed = _loginVerificationTokens.TryRemove(key, out _);
            return Task.FromResult(removed);
        }

        private static string GetRefreshTokenKey(string userId, string browserId)
        {
            return $"refresh:{userId}:{browserId}";
        }

        private static string GetLoginVerificationKey(string email, string browserId)
        {
            return $"verification:{email}:{browserId}";
        }
    }
}
