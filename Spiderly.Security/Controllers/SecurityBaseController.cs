using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.DTO;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Helpers;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.SecurityControllers // Needs to be other namespace because of source generator
{
    /// <summary>
    /// A base controller providing core security functionalities such as authentication, user management, and role-based access control.
    /// It leverages various services for handling user authentication (login, registration, logout, token refresh).    /// This controller is designed to be extended for specific user types.
    /// </summary>
    /// <typeparam name="TUser">The type of the user entity, which must implement the <see cref="IUser"/> interface.</typeparam>
    /// <typeparam name="TRole">The type of the role entity, which must implement the <see cref="IRole"/> interface.</typeparam>
    public class SecurityBaseController<TUser, TRole> : SpiderlyBaseController
        where TUser : class, IUser, new()
        where TRole : class, IRole, new()
    {
        private readonly SecurityServiceBase<TUser> _securityServiceBase;
        private readonly IJwtAuthManager _jwtAuthManagerService;
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;
        private readonly AuthorizationServiceBase _authorizationServiceBase;

        public SecurityBaseController(
            SecurityServiceBase<TUser> securityServiceBase,
            IJwtAuthManager jwtAuthManagerService,
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            AuthorizationServiceBase authorizationServiceBase
        )
        {
            _securityServiceBase = securityServiceBase;
            _jwtAuthManagerService = jwtAuthManagerService;
            _context = context;
            _authenticationService = authenticationService;
            _authorizationServiceBase = authorizationServiceBase;
        }

        #region Authentication

        [HttpPost]
        public async Task SendLoginVerificationEmail(LoginDTO loginDTO)
        {
            await _securityServiceBase.SendLoginVerificationEmail(loginDTO);
        }

        [HttpPost]
        public virtual async Task<AuthResultDTO> Login(VerificationTokenRequestDTO request)
        {
            return await _securityServiceBase.Login(request);
        }

        [HttpPost]
        [UIDoNotGenerate]
        public virtual async Task<AuthResultDTO> LoginExternal(ExternalProviderDTO externalProviderDTO) // TODO: Add enum for which external provider you should login user
        {
            return await _securityServiceBase.LoginExternal(externalProviderDTO);
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
            return await _securityServiceBase.RefreshToken(request);
        }

        #endregion

        #region User

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserBaseDTO> GetCurrentUserBase()
        {
            return await _authenticationService.GetCurrentUserBaseDTO<TUser>();
        }

        [HttpGet]
        [AuthGuard]
        [UIDoNotGenerate]
        public virtual async Task<List<string>> GetCurrentUserPermissionCodes()
        {
            return await _authorizationServiceBase.GetCurrentUserPermissionCodes<TUser, TRole>();
        }

        #endregion

    }
}
