using FluentValidation;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Spiderly.Security.DTO;
using Spiderly.Security.Entities;
using Spiderly.Security.Enums;
using Spiderly.Security.Interfaces;
using Spiderly.Security.ValidationRules;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Excel;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Resources;
using System.Security.Claims;

namespace Spiderly.Security.Services
{
    /// <summary>
    /// Provides business logic for security-related operations, including authentication, registration,
    /// token management, and user and role management. It leverages various services like JWT authentication,
    /// email sending, and data access through Entity Framework Core.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    public class SecurityBusinessService<TUser> : BusinessServiceGenerated<TUser> where TUser : class, IUser, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly AuthenticationService _authenticationService;
        private readonly AuthorizationBusinessService<TUser> _authorizationService;
        private readonly IEmailingService _emailingService;

        public SecurityBusinessService(
            IApplicationDbContext context,
            IJwtAuthManager jwtAuthManagerService,
            IEmailingService emailingService,
            AuthenticationService authenticationService,
            AuthorizationBusinessService<TUser> authorizationService,
            ExcelService excelService,
            IFileManager fileManager
        )
            : base(context, excelService, authorizationService, fileManager)
        {
            _context = context;
            _jwtAuthManagerService = jwtAuthManagerService;
            _emailingService = emailingService;
            _authenticationService = authenticationService;
            _authorizationService = authorizationService;
        }

        #region Authentication

        #region Login

        public async Task SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            new LoginDTOValidationRules().ValidateAndThrow(loginDTO);

            string userEmail = null;
            long userId = 0;

            await _context.WithTransactionAsync(async () =>
            {
                TUser user = await Authenticate(loginDTO);
                userEmail = user.Email;
                userId = user.Id;
            });

            string verificationCode = await _jwtAuthManagerService.GenerateAndSaveLoginVerificationCodeAsync(userEmail, userId, loginDTO.BrowserId);
            EmailVerifyUIDTO emailTemplate = CreateLoginEmailTemplate(verificationCode);

            try
            {
                await _emailingService.SendVerificationEmailAsync(userEmail, emailTemplate);
            }
            catch (Exception)
            {
                await _jwtAuthManagerService.RemoveLoginVerificationTokensByEmailAsync(userEmail); // We didn't send email, set all verification tokens invalid then
                throw;
            }
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
                TUser user = await GetUserByEmailAsync(loginVerificationTokenDTO.Email); // Check if user already exist in the database
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
                    AccessToken = jwtAuthResultDTO.AccessToken,
                    RefreshToken = jwtAuthResultDTO.Token.TokenString,
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
                TUser user = await GetUserByEmailAsync(payload.Email); // Check if user already exist in the database
                DbSet<TUser> userDbSet = _context.DbSet<TUser>();

                if (user == null)
                {
                    if (SettingsProvider.Current.OnlyAdminCanAddUsers)
                        throw new BusinessException(SharedTerms.AuthenticationEmailDoesNotExistException);

                    user = new TUser
                    {
                        Email = payload.Email,
                        HasLoggedInWithExternalProvider = true,
                    };

                    await userDbSet.AddAsync(user);
                    await _context.SaveChangesAsync(); // Adding the new user which is logged in first time
                }
                else
                {
                    if (user.IsDisabled == true)
                        throw new BusinessException(SharedTerms.DisabledAccountException);

                    if (user.HasLoggedInWithExternalProvider != true)
                        await userDbSet.ExecuteUpdateAsync(x => x.SetProperty(x => x.HasLoggedInWithExternalProvider, true)); // There is no need for SaveChangesAsync because we don't need to update the version of the user
                }

                JwtAuthResultDTO jwtAuthResultDTO = await GenerateAccessAndRefreshTokens(user.Id, externalProviderDTO.BrowserId);

                AuthResultDTO authResultDTO = new AuthResultDTO
                {
                    UserId = user.Id,
                    Email = user.Email,
                    AccessToken = jwtAuthResultDTO.AccessToken,
                    RefreshToken = jwtAuthResultDTO.Token.TokenString,
                };

                await OnAfterLogin(authResultDTO);

                return authResultDTO;
            });
        }

        /// <summary>
        /// By default assigns admin role to the first user. This is a performance bottleneck.
        /// Override this method with an empty implementation once the first user has admin permissions.
        /// </summary>
        public virtual async Task OnAfterLogin(AuthResultDTO authResultDTO)
        {
            await AssignAdminRoleToFirstUser(authResultDTO.UserId);
        }

        #endregion

        #region Helpers

        public async Task<AuthResultDTO> RefreshToken(RefreshTokenRequestDTO refreshTokenRequestDTO)
        {
            if (string.IsNullOrWhiteSpace(refreshTokenRequestDTO.RefreshToken))
                throw new SecurityTokenException(SharedTerms.ExpiredRefreshTokenException); // It's not realy this reason, but it's easier then realy explaining the user what has happened, this could happen if he deleted the cache from the browser

            string accessToken = await _authenticationService.GetAccessTokenAsync();
            List<Claim> claims = await _jwtAuthManagerService.GetClaimsForTheAccessTokenAsync(refreshTokenRequestDTO, accessToken);

            long accesTokenUserId = long.Parse(claims.FirstOrDefault(x => x.Type == ClaimTypes.PrimarySid)?.Value);

            string emailFromTheDb = await GetUserEmailByIdAsync(accesTokenUserId);

            JwtAuthResultDTO jwtResult = await _jwtAuthManagerService.RefreshAsync(refreshTokenRequestDTO, accesTokenUserId);

            return new AuthResultDTO
            {
                UserId = jwtResult.UserId, // Here it will always be user, if there is not, it will break earlier
                Email = emailFromTheDb,
                AccessToken = jwtResult.AccessToken,
                RefreshToken = jwtResult.Token.TokenString
            };
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

        private async Task AssignAdminRoleToFirstUser(long userId)
        {
            bool isFirstUserEver = await _context.DbSet<TUser>().CountAsync() == 1;
            if (isFirstUserEver)
            {
                Role adminRole = await _context.DbSet<Role>().FirstOrDefaultAsync(x => x.Name == "Admin");
                if (adminRole != null)
                {
                    TUser user = await _context.DbSet<TUser>().FirstOrDefaultAsync(x => x.Id == userId);
                    if (user != null && !user.Roles.Any())
                    {
                        user.Roles.Add(adminRole);
                        await _context.SaveChangesAsync();
                    }
                }
            }
        }

        #endregion

        #endregion

        #region User

        public async Task<UserBaseDTO> GetCurrentUserBaseDTO()
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await _context.DbSet<TUser>()
                    .Where(x => x.Id == _authenticationService.GetCurrentUserId())
                    .Select(x => new UserBaseDTO
                    {
                        Id = x.Id,
                        Email = x.Email
                    })
                    .SingleOrDefaultAsync();
            });
        }

        public async Task<List<NamebookDTO<int>>> GetRolesNamebookListForUser(long userId, bool authorize = true)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _authorizationService.AuthorizeAndThrowAsync<TUser>(SecurityPermissionCodes.ReadUser);
                }

                return await _context.DbSet<TUser>()
                    .AsNoTracking()
                    .Where(x => x.Id == userId)
                    .SelectMany(x => x.Roles)
                    .Select(role => new NamebookDTO<int>
                    {
                        Id = role.Id,
                        DisplayName = role.Name,
                    })
                    .ToListAsync();
            });
        }

        public async Task UpdateRoleListForUser(long userId, List<int> selectedRoleIds)
        {
            await _context.WithTransactionAsync(async () =>
            {
                TUser user = await GetInstanceAsync<TUser, long>(userId, null);

                foreach (Role role in user.Roles.ToList())
                {
                    if (selectedRoleIds.Contains(role.Id))
                        selectedRoleIds.Remove(role.Id);
                    else
                        user.Roles.Remove(role);
                }

                List<Role> roleListToInsert = await _context.DbSet<Role>().Where(x => selectedRoleIds.Contains(x.Id)).ToListAsync();

                user.Roles.AddRange(roleListToInsert);
                await _context.SaveChangesAsync();
            });
        }

        #endregion

        #region Role

        public override async Task<RoleMainUIFormDTO> GetRoleMainUIFormDTO(int id, bool authorize)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _authorizationService.AuthorizeRoleReadAndThrow(id);
                }

                return new RoleMainUIFormDTO
                {
                    RoleDTO = await GetRoleDTO(id, false),
                    PermissionsIds = await GetPermissionsIdsForRole(id, false),
                    UsersNamebookDTOList = await GetUsersNamebookListForRole(id, false),
                };
            });
        }

        public override async Task<RoleMainUIFormDTO> SaveRoleAndReturnMainUIFormDTO(RoleSaveBodyDTO saveBodyDTO, bool authorizeUpdate, bool authorizeInsert)
        {
            RoleMainUIFormDTO roleMainUIFormDTO = await base.SaveRoleAndReturnMainUIFormDTO(saveBodyDTO, authorizeUpdate, authorizeInsert);
            roleMainUIFormDTO.UsersNamebookDTOList = saveBodyDTO.SelectedUsersNamebookDTOList;
            return roleMainUIFormDTO;
        }

        protected override async Task OnAfterSaveRoleAndReturnMainUIFormDTO(RoleDTO savedDTO, RoleSaveBodyDTO saveBodyDTO)
        {
            await _context.WithTransactionAsync(async () =>
            {
                await UpdateUsersForRole(savedDTO.Id, saveBodyDTO.SelectedUsersNamebookDTOList.Select(x => x.Id));
            });
        }

        public async Task UpdateUsersForRole(int roleId, IEnumerable<long> selectedUserIds)
        {
            if (selectedUserIds == null)
                return;

            HashSet<long> newUserIdSet = new HashSet<long>(selectedUserIds);
            List<UserRole> usersToRemove = new();
            List<UserRole> usersToAdd = new();

            await _context.WithTransactionAsync(async () =>
            {
                List<UserRole> existingRoleUsers = await _context
                    .DbSet<UserRole>()
                    .Where(x => x.RoleId == roleId)
                    .ToListAsync();

                foreach (UserRole existingRoleUser in existingRoleUsers)
                {
                    if (newUserIdSet.Contains(existingRoleUser.UserId))
                    {
                        newUserIdSet.Remove(existingRoleUser.UserId);
                    }
                    else
                    {
                        usersToRemove.Add(existingRoleUser);
                    }
                }

                foreach (long newUserId in newUserIdSet)
                {
                    usersToAdd.Add(new UserRole { RoleId = roleId, UserId = newUserId });
                }

                _context.DbSet<UserRole>().RemoveRange(usersToRemove);
                _context.DbSet<UserRole>().AddRange(usersToAdd);

                await _context.SaveChangesAsync();
            });
        }

        public async Task<List<NamebookDTO<long>>> GetUsersNamebookListForRole(long roleId, bool authorize = true)
        {
            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _authorizationService.AuthorizeAndThrowAsync<TUser>(SecurityPermissionCodes.ReadRole);
                }

                return await _context.DbSet<TUser>()
                    .AsNoTracking()
                    .Where(x => x.Roles.Any(x => x.Id == roleId))
                    .Select(x => new NamebookDTO<long>
                    {
                        Id = x.Id,
                        DisplayName = x.Email,
                    })
                    .ToListAsync();
            });
        }

        public async Task<List<NamebookDTO<long>>> GetUsersAutocompleteListForRole(int limit, string filter, bool authorize)
        {
            IQueryable<TUser> query = _context.DbSet<TUser>();

            return await _context.WithTransactionAsync(async () =>
            {
                if (authorize)
                {
                    await _authorizationService.AuthorizeAndThrowAsync<TUser>(SecurityPermissionCodes.ReadRole);
                }

                if (!string.IsNullOrEmpty(filter))
                    query = query.Where(x => x.Email.Contains(filter));

                return await query
                    .AsNoTracking()
                    .Take(limit)
                    .Select(x => new NamebookDTO<long>
                    {
                        Id = x.Id,
                        DisplayName = x.Email,
                    })
                    .ToListAsync();
            });
        }

        #endregion
    }
}
