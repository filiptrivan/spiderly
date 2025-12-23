using Spiderly.Security.DTO;
using System.Collections.Immutable;
using System.Security.Claims;

namespace Spiderly.Security.Interfaces
{
    // TODO FT: Sort the arguments of the methods
    public interface IJwtAuthManager
    {
        Task<IImmutableDictionary<string, RefreshTokenDTO>> GetUsersRefreshTokensReadOnlyDictionaryAsync();
        Task<IImmutableDictionary<string, LoginVerificationTokenDTO>> GetUsersLoginVerificationTokensReadOnlyDictionaryAsync();

        #region Refresh

        Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokensAsync(long userId, string ipAddress, string browserId);
        List<Claim> GenerateClaims(long userId);
        Task<JwtAuthResultDTO> RefreshAsync(RefreshTokenRequestDTO request, long dbUserId);
        Task<List<Claim>> GetClaimsForTheAccessTokenAsync(RefreshTokenRequestDTO request, string accessToken);
        Task RemoveExpiredRefreshTokensAsync();
        Task RemoveRefreshTokenByUserIdAsync(long userId);
        Task LogoutAsync(string browserId, long userId);
        Task<bool> RemoveLastRefreshTokenFromTheSameBrowserAndUserIdAsync(string browserId, long userId);

        #endregion

        #region Login verification

        Task<LoginVerificationTokenDTO> ValidateAndGetLoginVerificationTokenDTOAsync(string verificationToken, string browserId, string email);
        Task<string> GenerateAndSaveLoginVerificationCodeAsync(string userEmail, long userId, string browserId);
        Task RemoveLoginVerificationTokensByEmailAsync(string email);

        #endregion

    }
}