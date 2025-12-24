using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.DTO;
using Spiderly.Security.Entities;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.DTO;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.SecurityControllers // Needs to be other namespace because of source generator
{
    /// <summary>
    /// A base controller providing core security functionalities such as authentication, user management, and role-based access control.
    /// It leverages various services for handling user authentication (login, registration, logout, token refresh),
    /// retrieving user information and permissions, and managing roles (CRUD operations, assigning users and permissions).
    /// This controller is designed to be extended for specific user types.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    public class SecurityBaseController<TUser> : SpiderlyBaseController where TUser : class, IUser, new()
    {
        private readonly SecurityBusinessService<TUser> _securityBusinessService;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly AuthorizationService _authorizationService;

        public SecurityBaseController(
            SecurityBusinessService<TUser> securityBusinessService,
            IJwtAuthManager jwtAuthManagerService,
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService
        )
        {
            _securityBusinessService = securityBusinessService;
            _jwtAuthManagerService = jwtAuthManagerService;
            _context = context;
            _authenticationService = authenticationService;
            _authorizationService = authorizationService;
        }

        #region Authentication

        [HttpPost]
        public async Task SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            await _securityBusinessService.SendLoginVerificationEmail(loginDTO);
        }

        [HttpPost]
        public virtual async Task<AuthResultDTO> Login(VerificationTokenRequestDTO request)
        {
            return await _securityBusinessService.Login(request);
        }

        [HttpPost]
        [UIDoNotGenerate]
        public virtual async Task<AuthResultDTO> LoginExternal(ExternalProviderDTO externalProviderDTO) // TODO: Add enum for which external provider you should login user
        {
            return await _securityBusinessService.LoginExternal(externalProviderDTO);
        }

        [HttpGet]
        [AuthGuard]
        public async Task<ActionResult> Logout(string browserId)
        {
            long userId = _authenticationService.GetCurrentUserId();
            await _jwtAuthManagerService.LogoutAsync(browserId, userId); // If the malicious user is deleting browser id, and sending request with refresh token like that we will delete every refresh token for that user

            return Ok();
        }

        /// <summary>
        /// Here we would put [Authorize] attribute, because we don't validate life time of the access token, but we are not because deeper in the method we are validating it without life time also. 
        /// </summary>
        [HttpPost]
        public async Task<AuthResultDTO> RefreshToken(RefreshTokenRequestDTO request)
        {
            return await _securityBusinessService.RefreshToken(request);
        }

        #endregion

        #region User

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserBaseDTO> GetCurrentUserBase()
        {
            return await _securityBusinessService.GetCurrentUserBaseDTO();
        }

        [HttpGet]
        [AuthGuard]
        [UIDoNotGenerate]
        public virtual async Task<List<string>> GetCurrentUserPermissionCodes()
        {
            return await _authorizationService.GetCurrentUserPermissionCodes<TUser>();
        }

        #endregion

        #region Role

        [HttpPost]
        [AuthGuard]
        public async Task<PaginatedResultDTO<RoleDTO>> GetPaginatedRoleList(FilterDTO filterDTO)
        {
            return await _securityBusinessService.GetPaginatedRoleList(filterDTO, _context.DbSet<Role>(), true);
        }

        [HttpPost]
        [AuthGuard]
        public async Task<IActionResult> ExportRoleListToExcel(FilterDTO filterDTO)
        {
            byte[] fileContent = await _securityBusinessService.ExportRoleListToExcel(filterDTO, _context.DbSet<Role>(), true);
            return File(fileContent, SettingsProvider.Current.ExcelContentType, Uri.EscapeDataString($"Roles.xlsx"));
        }

        [HttpDelete]
        [AuthGuard]
        public async Task DeleteRole(int id)
        {
            await _securityBusinessService.DeleteRole(id, true);
        }

        [HttpGet]
        [AuthGuard]
        public async Task<RoleMainUIFormDTO> GetRoleMainUIFormDTO(int id)
        {
            return await _securityBusinessService.GetRoleMainUIFormDTO(id, true);
        }

        [HttpGet]
        [AuthGuard]
        public async Task<RoleDTO> GetRole(int id)
        {
            return await _securityBusinessService.GetRoleDTO(id, true);
        }

        [HttpPut]
        [AuthGuard]
        public async Task<RoleMainUIFormDTO> SaveRole(RoleSaveBodyDTO saveBodyDTO)
        {
            return await _securityBusinessService.SaveRoleAndReturnMainUIFormDTO(saveBodyDTO, true, true);
        }

        [HttpGet]
        [AuthGuard]
        public async Task<List<NamebookDTO<long>>> GetUsersNamebookListForRole(int roleId)
        {
            return await _securityBusinessService.GetUsersNamebookListForRole(roleId);
        }

        [HttpGet]
        [AuthGuard]
        public async Task<List<NamebookDTO<int>>> GetPermissionsDropdownListForRole()
        {
            return await _securityBusinessService.GetPermissionsDropdownListForRole(_context.DbSet<Permission>(), true, null);
        }

        [HttpGet]
        [AuthGuard]
        public virtual async Task<List<NamebookDTO<long>>> GetUsersAutocompleteListForRole(int limit, string query, long roleId)
        {
            return await _securityBusinessService.GetUsersAutocompleteListForRole(limit, query, true);
        }

        #endregion

        #region Permission

        [HttpGet]
        [AuthGuard]
        public async Task<List<NamebookDTO<int>>> GetPermissionsNamebookListForRole(int roleId)
        {
            return await _securityBusinessService.GetPermissionsNamebookListForRole(roleId, true);
        }

        #endregion

    }
}
