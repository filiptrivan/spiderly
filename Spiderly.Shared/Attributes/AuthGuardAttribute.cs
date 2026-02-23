using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// <b>Usage:</b> Provides authentication protection for API endpoints by validating JWT tokens in the request.
    /// </summary>
    public class AuthGuardAttribute : ActionFilterAttribute
    {

        public AuthGuardAttribute()
        {
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            string accessToken = Helper.GetAccessTokenFromHeader(context.HttpContext)
                ?? Helper.GetAccessTokenFromCookie(context.HttpContext);

            if (string.IsNullOrEmpty(accessToken))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (Helper.IsJwtTokenValid(accessToken) == false)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            base.OnActionExecuting(context);
        }

    }
}