using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            string accessTokenKey = context.HttpContext.RequestServices.GetRequiredService<IOptions<TokenKeyOptions>>().Value.AccessTokenKey;
            string accessTokenFromHeader = Helper.GetAccessTokenFromHeader(context.HttpContext);
            string accessTokenFromCookie = Helper.GetAccessTokenFromCookie(context.HttpContext, accessTokenKey);
            bool authenticatedViaCookie = accessTokenFromHeader == null && accessTokenFromCookie != null;

            string method = context.HttpContext.Request.Method;
            bool isStateChangingMethod = method != "GET" && method != "HEAD" && method != "OPTIONS";

            if (isStateChangingMethod && authenticatedViaCookie)
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