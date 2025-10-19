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

        JwtAuthResultDTO GenerateAccessAndRefreshTokens(long userId, string userEmail, string ipAddress, string browserId);
        List<Claim> GenerateClaims(long userId, string userEmail);
        JwtAuthResultDTO Refresh(RefreshTokenRequestDTO request, long dbUserId, string dbUserEmail);
        List<Claim> GetClaimsForTheAccessToken(RefreshTokenRequestDTO request, string accessToken);
        void RemoveExpiredRefreshTokens();
        void RemoveRefreshTokenByEmail(string email);
        public void Logout(string browserId, string email);
        bool RemoveLastRefreshTokenFromTheSameBrowserAndEmail(string browserId, string email);

        #endregion

        #region Login verification

        LoginVerificationTokenDTO ValidateAndGetLoginVerificationTokenDTO(string verificationToken, string browserId, string email);
        string GenerateAndSaveLoginVerificationCode(string userEmail, long userId, string browserId);
        void RemoveLoginVerificationTokensByEmail(string email);

        #endregion

    }
}