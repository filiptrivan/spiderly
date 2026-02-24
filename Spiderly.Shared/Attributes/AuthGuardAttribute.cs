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
            string accessTokenFromHeader = Helper.GetAccessTokenFromHeader(context.HttpContext);
            string accessTokenFromCookie = Helper.GetAccessTokenFromCookie(context.HttpContext);

            string accessToken = accessTokenFromHeader ?? accessTokenFromCookie;

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

            string method = context.HttpContext.Request.Method;
            bool isStateChangingMethod = method != "GET" && method != "HEAD" && method != "OPTIONS";

            if (isStateChangingMethod && accessTokenFromHeader == null)
            {
                if (!context.HttpContext.Request.Headers.ContainsKey("X-CSRF"))
                {
                    context.Result = new ForbidResult();
                    return;
                }
            }

            base.OnActionExecuting(context);
        }

    }
}