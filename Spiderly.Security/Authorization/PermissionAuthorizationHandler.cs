using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Spiderly.Security.Services;
using Spiderly.Shared.Authorization;

namespace Spiderly.Security.Authorization
{
    /// <summary>
    /// Evaluates <see cref="PermissionRequirement"/> by delegating to the registered
    /// <see cref="AuthorizationServiceBase.IsAuthorizedAsync(string)"/> — so the principal-kind dispatch and any
    /// consumer override (e.g. an API-key role cap) apply unchanged. Fails closed: an anonymous caller or a
    /// missing permission simply does not succeed the requirement (no <c>context.Fail()</c>, leaving room for
    /// other handlers in a composed policy).
    /// </summary>
    public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
    {
        private readonly AuthorizationServiceBase _authorizationService;

        /// <summary>Creates the handler over the registered authorization service (the consumer's most-derived registration).</summary>
        /// <param name="authorizationService">The authorization service whose <c>IsAuthorizedAsync</c> decides the requirement.</param>
        public PermissionAuthorizationHandler(AuthorizationServiceBase authorizationService)
        {
            _authorizationService = authorizationService;
        }

        /// <inheritdoc/>
        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
        {
            // Anonymous → deny without resolving a principal (avoids throwing on a null current user; the
            // materialized policy also requires an authenticated user, so this is belt-and-suspenders).
            if (context.User?.Identity?.IsAuthenticated != true)
                return;

            if (await _authorizationService.IsAuthorizedAsync(requirement.PermissionCode))
                context.Succeed(requirement);
        }
    }
}
