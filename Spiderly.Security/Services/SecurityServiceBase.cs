using FluentValidation;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.ValidationRules;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Resources;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides business logic for security-related operations, including authentication, registration,
    /// and token management. It leverages various services like JWT authentication,
    /// email sending, and data access through Entity Framework Core.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    public class SecurityServiceBase<TUser> where TUser : class, IUser, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly AuthenticationService _authenticationService;
        private readonly IEmailingService _emailingService;
        private readonly IWebHostEnvironment _environment;

        public SecurityServiceBase(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManagerService,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            IWebHostEnvironment environment
        )
        {
            _context = context;
            _jwtAuthManagerService = jwtAuthManagerService;
            _emailingService = emailingService;
            _authenticationService = authenticationService;
            _environment = environment;
        }

        #region Authentication

        #region Login

        public async Task<SendLoginVerificationEmailResultDTO> SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            new LoginDTOValidationRules().ValidateAndThrow(loginDTO);

            TUser user = await Authenticate(loginDTO);

            string userEmail;

            if (user == null)
            {
                if (SettingsProvider.Current.OnlyAdminCanAddUsers)
                    throw new BusinessException(SharedTerms.AuthenticationEmailDoesNotExistException);

                userEmail = loginDTO.Email;
            }
            else
            {
                userEmail = user.Email;
            }

            string verificationCode = await _jwtAuthManagerService.GenerateAndSaveLoginVerificationCodeAsync(userEmail, loginDTO.BrowserId);

            if (ShouldShowVerificationCodeInNotification())
            {
                return new SendLoginVerificationEmailResultDTO
                {
                    Message = string.Format(SharedTerms.VerificationCodeDevelopmentMode, verificationCode),
                    VerificationCode = verificationCode
                };
            }

            EmailVerifyUIDTO emailTemplate = CreateLoginEmailTemplate(verificationCode);

            try
            {
                await _emailingService.SendVerificationEmailAsync(userEmail, emailTemplate);
            }
            catch (Exception)
            {
                // We didn't send email, set all verification tokens invalid then
                await _jwtAuthManagerService.RemoveLoginVerificationTokensByEmailAsync(userEmail);
                throw;
            }

            return new SendLoginVerificationEmailResultDTO
            {
                Message = string.Empty
            };
        }

        public virtual EmailVerifyUIDTO CreateLoginEmailTemplate(string verificationCode)
        {
            return new EmailVerifyUIDTO
            {
                Subject = SharedTerms.EmailAccountVerificationTitle,
                Body = verificationCode
            };
        }

        public async Task<AuthResultDTO> Login(VerificationTokenRequestDTO verificationRequestDTO)
        {
            new VerificationTokenRequestDTOValidationRules().ValidateAndThrow(verificationRequestDTO);

            // Can not be null, if its null it already has thrown
            LoginVerificationTokenDTO loginVerificationTokenDTO = await _jwtAuthManagerService.ValidateAndGetLoginVerificationTokenDTOAsync(
                verificationRequestDTO.VerificationCode, verificationRequestDTO.BrowserId, verificationRequestDTO.Email);

            return await _context.WithTransactionAsync(async () =>
            {
                // Check if the user already exists in the database
                TUser user = await GetUserByEmailAsync(loginVerificationTokenDTO.Email);
                DbSet<TUser> userDbSet = _context.DbSet<TUser>();

                if (user == null)
                {
                    if (SettingsProvider.Current.OnlyAdminCanAddUsers)
                        throw new BusinessException(SharedTerms.AuthenticationEmailDoesNotExistException);

                    user = new TUser
                    {
                        Email = loginVerificationTokenDTO.Email,
                    };

                    await userDbSet.AddAsync(user);
                    await _context.SaveChangesAsync(); // Adding the new user which is logged in first time
                }
                else
                {
                    if (user.IsDisabled == true)
                        throw new BusinessException(SharedTerms.DisabledAccountException);
                }

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, loginVerificationTokenDTO.BrowserId);

                AuthResultDTO authResultDTO = new AuthResultDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    AccessToken = jwtAuthResultDTO.AccessTokenDTO.TokenString,
                    AccessTokenExpiresAt = jwtAuthResultDTO.AccessTokenDTO.ExpiresAt,
                    RefreshToken = jwtAuthResultDTO.RefreshTokenDTO.TokenString,
                };

                await OnAfterLogin(authResultDTO);

                return authResultDTO;
            });
        }

        public async Task<AuthResultDTO> LoginExternal(ExternalProviderDTO externalProviderDTO)
        {
            string googleClientId = SettingsProvider.Current.GoogleClientId;

            GoogleJsonWebSignature.Payload payload = await ValidateGoogleToken(externalProviderDTO.IdToken, googleClientId);

            return await _context.WithTransactionAsync(async () =>
            {
                // Check if the user already exists in the database
                TUser user = await GetUserByEmailAsync(payload.Email);
                DbSet<TUser> userDbSet = _context.DbSet<TUser>();

                if (user == null)
                {
                    if (SettingsProvider.Current.OnlyAdminCanAddUsers)
                        throw new BusinessException(SharedTerms.AuthenticationEmailDoesNotExistException);

                    user = new TUser
                    {
                        Email = payload.Email,
                        HasLoggedInWithGoogleAsExternalProvider = true,
                    };

                    await userDbSet.AddAsync(user);
                    await _context.SaveChangesAsync(); // Adding the new user which is logged in first time
                }
                else
                {
                    if (user.IsDisabled == true)
                        throw new BusinessException(SharedTerms.DisabledAccountException);

                    if (user.HasLoggedInWithGoogleAsExternalProvider != true)
                        await userDbSet.ExecuteUpdateAsync(x => x.SetProperty(x => x.HasLoggedInWithGoogleAsExternalProvider, true)); // There is no need for SaveChangesAsync because we don't need to update the version of the user
                }

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, externalProviderDTO.BrowserId);

                AuthResultDTO authResultDTO = new AuthResultDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    AccessToken = jwtAuthResultDTO.AccessTokenDTO.TokenString,
                    AccessTokenExpiresAt = jwtAuthResultDTO.AccessTokenDTO.ExpiresAt,
                    RefreshToken = jwtAuthResultDTO.RefreshTokenDTO.TokenString,
                };

                await OnAfterLogin(authResultDTO);

                return authResultDTO;
            });
        }

        public virtual async Task OnAfterLogin(AuthResultDTO authResultDTO) { }

        public async Task<AuthResultWithCookiesDTO> LoginWithCookies(VerificationTokenRequestDTO verificationRequestDTO)
        {
            AuthResultDTO authResultDTO = await Login(verificationRequestDTO);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        public async Task<AuthResultWithCookiesDTO> LoginExternalWithCookies(ExternalProviderDTO externalProviderDTO)
        {
            AuthResultDTO authResultDTO = await LoginExternal(externalProviderDTO);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        #endregion

        #region Helpers

        public async Task<AuthResultDTO> RefreshToken(RefreshTokenRequestDTO refreshTokenRequestDTO, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRequestDTO.RefreshToken))
                throw new SecurityTokenException(SharedTerms.ExpiredRefreshTokenException); // It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            List<Claim> claims = await _jwtAuthManagerService.GetClaimsForTheAccessTokenAsync(refreshTokenRequestDTO, accessToken);

            long accesTokenUserId = long.Parse(claims.FirstOrDefault(x => x.Type == ClaimTypes.PrimarySid)?.Value);

            string emailFromTheDb = await GetUserEmailByIdAsync(accesTokenUserId);

            JwtAuthResultDTO jwtResult = await _jwtAuthManagerService.RefreshAsync(refreshTokenRequestDTO, accesTokenUserId);

            return new AuthResultDTO
            {
                UserId = jwtResult.UserId, // Here it will always be user, if there is not, it will break earlier
                Email = emailFromTheDb,
                AccessToken = jwtResult.AccessTokenDTO.TokenString,
                AccessTokenExpiresAt = jwtResult.AccessTokenDTO.ExpiresAt,
                RefreshToken = jwtResult.RefreshTokenDTO.TokenString
            };
        }

        public async Task<AuthResultDTO> RefreshTokenWithHeaders(RefreshTokenRequestDTO refreshTokenRequestDTO)
        {
            string accessToken = _authenticationService.GetAccessTokenFromHeader();
            return await RefreshToken(refreshTokenRequestDTO, accessToken);
        }

        public async Task<AuthResultWithCookiesDTO> RefreshTokenWithCookies(string browserId)
        {
            string refreshToken = _authenticationService.GetRefreshTokenFromCookie();
            string accessToken = _authenticationService.GetAccessTokenFromCookie();

            RefreshTokenRequestDTO refreshTokenRequestDTO = new RefreshTokenRequestDTO
            {
                RefreshToken = refreshToken,
                BrowserId = browserId
            };

            AuthResultDTO authResultDTO = await RefreshToken(refreshTokenRequestDTO, accessToken);

            AuthResultWithCookiesDTO authResultWithCookiesDTO = new AuthResultWithCookiesDTO
            {
                userId = authResultDTO.UserId,
                email = authResultDTO.Email,
                accessTokenExpiresAt = authResultDTO.AccessTokenExpiresAt,
            };

            _authenticationService.SetRefreshTokenCookie(authResultDTO.RefreshToken);
            _authenticationService.SetAccessTokenCookie(authResultDTO.AccessToken);
            _authenticationService.SetAuthResultCookie(authResultWithCookiesDTO);

            return authResultWithCookiesDTO;
        }

        public async Task<string> GetUserEmailByIdAsync(long id)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Id == id).Select(x => x.Email).SingleOrDefaultAsync();
            });
        }

        public async Task<TUser> GetUserByEmailAsync(string email)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>().AsNoTracking().Where(x => x.Email == email).SingleOrDefaultAsync();
            });
        }

        private async Task<JwtAuthResultDTO> GenerateAccessAndRefreshTokens(long userId, string browserId)
        {
            string ipAddress = _authenticationService.GetIPAddress();

            JwtAuthResultDTO jwtAuthResult = await _jwtAuthManagerService.GenerateAccessAndRefreshTokensAsync(userId, ipAddress, browserId);

            return jwtAuthResult;
        }

        private async Task<TUser> Authenticate(LoginDTO loginDTO)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                TUser currentUser = await _context.DbSet<TUser>()
                    .Where(x => x.Email == loginDTO.Email)
                    .SingleOrDefaultAsync();

                if (currentUser == null)
                    return null;

                if (currentUser.IsDisabled == true)
                    throw new BusinessException(SharedTerms.DisabledAccountException);

                return currentUser;
            });
        }

        private async Task<GoogleJsonWebSignature.Payload> ValidateGoogleToken(string idToken, string clientId)
        {
            GoogleJsonWebSignature.ValidationSettings settings = new GoogleJsonWebSignature.ValidationSettings()
            {
                Audience = new List<string>() { clientId }
            };

            GoogleJsonWebSignature.Payload payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings); // TODO: Try to pass the wrong token
            return payload;
        }

        private bool ShouldShowVerificationCodeInNotification()
        {
            bool isEmailingConfigured = Shared.Helpers.Helper.IsEmailingConfigured();
            bool isDevelopment = _environment.IsDevelopment();

            return isDevelopment && !isEmailingConfigured;
        }

        #endregion

        #endregion

    }
}
