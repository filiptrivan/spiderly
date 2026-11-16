using Spiderly.Shared.Classes;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The WebAPI project's <c>Controllers/</c> folder: the security and user endpoints, and the outbox row actions.
    /// </summary>
    internal static class WebApiControllersTemplate
    {
        internal static SpiderlyFolder Folder(string appName) => new()
        {
            Name = "Controllers",
            Files =
            {
                new SpiderlyFile { Name = "SecurityController.cs", Data = GetSecurityControllerCsData(appName) },
                new SpiderlyFile { Name = "UserController.cs", Data = GetUserControllerCsData(appName) },
                new SpiderlyFile { Name = "OutboxMessageController.cs", Data = GetOutboxMessageControllerCsData(appName) },
            }
        };

        private static string GetSecurityControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spiderly.Security.Interfaces;
using Spiderly.Security.Services;
using Spiderly.Security.SecurityControllers;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.DTO;
using Microsoft.EntityFrameworkCore;
using Spiderly.Security.DTO;
using Spiderly.Shared.Extensions;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;

namespace {{appName}}.WebAPI.Controllers
{
    [SpiderlyController]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class SecurityController : SecurityBaseController<User, Role, UserExternalLogin>
    {
        private readonly ILogger<SecurityController> _logger;
        private readonly SecurityService<User, UserExternalLogin> _securityService;
        private readonly IApplicationDbContext _context;

        public SecurityController(
            ILogger<SecurityController> logger,
            SecurityService<User, UserExternalLogin> securityService,
            IJwtAuthManager jwtAuthManagerService,
            IApplicationDbContext context,
            AuthenticationService authenticationService,
            AuthorizationService authorizationService
        )
            : base(securityService, jwtAuthManagerService, context, authenticationService, authorizationService)
        {
            _logger = logger;
            _securityService = securityService;
            _context = context;
        }

    }
}

""";
        }

        private static string GetUserControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.DTO;
using Spiderly.Security.Services;
using Microsoft.Extensions.Localization;
using {{appName}}.Business.Services;
using {{appName}}.Business.DTO;
using {{appName}}.Business.Entities;

namespace {{appName}}.WebAPI.Controllers
{
    [SpiderlyController]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    public class UserController : UserBaseController
    {
        private readonly UserServiceGenerated _userService;
        private readonly AuthenticationService _authenticationService;

        public UserController(
            IApplicationDbContext context,
            IServiceProvider serviceProvider,
            UserServiceGenerated userService,
            AuthenticationService authenticationService,
            IStringLocalizer localizer
        )
            : base(context, serviceProvider, localizer)
        {
            _userService = userService;
            _authenticationService = authenticationService;
        }

        [HttpGet]
        [AuthGuard]
        [SkipSpinner]
        public async Task<UserDTO> GetCurrentUser()
        {
            long userId = _authenticationService.GetCurrentUserId();
            return await _userService.GetUserDTO(userId); // Current user reading own profile; admin reads are gated by [AuthGuard("ReadUser")] on the generated endpoint
        }

    }
}

""";
        }

        private static string GetOutboxMessageControllerCsData(string appName)
        {
            return $$"""
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using {{appName}}.Business.Entities;
using {{appName}}.Business.Enums;
using {{appName}}.Business.Services;
using Spiderly.Security.Services;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Attributes.Entity.UI;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Interfaces;

namespace {{appName}}.WebAPI.Controllers
{
    /// <summary>
    /// Admin row actions on the OutboxMessage table beyond the generated read:
    /// <list type="bullet">
    /// <item><description><c>Retry</c> — clears AttemptCount + LastError + NextAttemptAt so the next OutboxDispatcherJob
    /// sweep picks the row up (e.g. after an external dependency that was down recovers).</description></item>
    /// <item><description><c>Dismiss</c> — marks the row handled out-of-band (sets DispatchedAt +
    /// DismissedByUserId); the sweep skips it.</description></item>
    /// </list>
    /// Both require the generated UpdateOutboxMessage permission. Insert/Delete are intentionally never granted —
    /// rows are produced by IOutbox and consumed by OutboxDispatcherJob.
    /// </summary>
    [UIDoNotGenerate]
    [ApiController]
    [Route("/api/[controller]/[action]")]
    [SpiderlyController]
    public class OutboxMessageController : ControllerBase
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthorizationService _authorizationService;
        private readonly AuthenticationService _authenticationService;
        private readonly TimeProvider _timeProvider;

        public OutboxMessageController(
            IApplicationDbContext context,
            AuthorizationService authorizationService,
            AuthenticationService authenticationService,
            TimeProvider timeProvider)
        {
            _context = context;
            _authorizationService = authorizationService;
            _authenticationService = authenticationService;
            _timeProvider = timeProvider;
        }

        [HttpPost]
        [AuthGuard]
        public async Task<IActionResult> Retry(long id)
        {
            await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.UpdateOutboxMessage);

            OutboxMessage? row = await _context.DbSet<OutboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
                throw new BusinessException("Message not found.");

            // Fail loud rather than silently re-emit a side effect that already happened.
            if (row.DispatchedAt != null)
                throw new BusinessException("Message already dispatched or dismissed — cannot retry.");

            row.AttemptCount = 0;
            row.LastError = null;
            row.LastAttemptedAt = null;
            // Clear the backoff/dead-letter gate so the next sweep picks the row up immediately.
            row.NextAttemptAt = null;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost]
        [AuthGuard]
        public async Task<IActionResult> Dismiss(long id)
        {
            await _authorizationService.AuthorizeAndThrowAsync(PermissionCodes.UpdateOutboxMessage);

            OutboxMessage? row = await _context.DbSet<OutboxMessage>()
                .FirstOrDefaultAsync(x => x.Id == id);

            if (row == null)
                throw new BusinessException("Message not found.");

            // Idempotent: dismissing an already-dispatched/dismissed row is a no-op.
            if (row.DispatchedAt != null)
                return Ok();

            long currentUserId = _authenticationService.GetCurrentUserId();
            row.DispatchedAt = _timeProvider.GetUtcNow().UtcDateTime;
            row.DismissedByUserId = currentUserId;
            await _context.SaveChangesAsync();

            return Ok();
        }
    }
}
""";
        }
    }
}
