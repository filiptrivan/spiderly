using Spiderly.Security.DTO;
using System.Collections.Immutable;
using System.Security.Claims;

namespace Spiderly.Security.Interfaces
{
    // TODO FT: Sort the arguments of the methods
    public interface IJwtAuthManager
    {
        IImmutableDictionary<string, RefreshTokenDTO> UsersRefreshTokensReadOnlyDictionary { get; }
        IImmutableDictionary<string, LoginVerificationTokenDTO> UsersLoginVerificationTokensReadOnlyDictionary { get; }

        #region Refresh

        JwtAuthResultDTO GenerateAccessAndRefreshTokens(long userId, string ipAddress, string browserId);
        List<Claim> GenerateClaims(long userId);
        JwtAuthResultDTO Refresh(RefreshTokenRequestDTO request, long dbUserId);
        List<Claim> GetClaimsForTheAccessToken(RefreshTokenRequestDTO request, string accessToken);
        void RemoveExpiredRefreshTokens();
        void RemoveRefreshTokenByUserId(long userId);
        public void Logout(string browserId, long userId);
        bool RemoveLastRefreshTokenFromTheSameBrowserAndUserId(string browserId, long userId);

        #endregion

        #region Login verification

        LoginVerificationTokenDTO ValidateAndGetLoginVerificationTokenDTO(string verificationToken, string browserId, string email);
        string GenerateAndSaveLoginVerificationCode(string userEmail, long userId, string browserId);
        void RemoveLoginVerificationTokensByEmail(string email);

        #endregion

    }
}