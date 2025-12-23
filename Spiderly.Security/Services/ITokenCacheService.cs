using Spiderly.Security.DTO;

namespace Spiderly.Security.Services
{
    public interface ITokenCacheService
    {
        Task<RefreshTokenDTO?> GetRefreshTokenAsync(string userId, string browserId);
        Task SetRefreshTokenAsync(string userId, string browserId, RefreshTokenDTO token, TimeSpan expiration);
        Task<bool> RemoveRefreshTokenAsync(string userId, string browserId);
        Task<IEnumerable<RefreshTokenDTO>> GetAllRefreshTokensForUserAsync(string userId);
        Task RemoveAllRefreshTokensForUserAsync(string userId);

        Task<LoginVerificationTokenDTO?> GetLoginVerificationTokenAsync(string email, string browserId);
        Task SetLoginVerificationTokenAsync(string email, string browserId, LoginVerificationTokenDTO token, TimeSpan expiration);
        Task<bool> RemoveLoginVerificationTokenAsync(string email, string browserId);
    }
}
