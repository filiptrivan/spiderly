using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Helpers;

namespace Spiderly.Shared.Attributes
{
    /// <summary>
    /// Requires an authenticated request before the decorated controller action runs. The filter accepts
    /// the standard Spiderly JWT token locations and, when a state-changing request is authenticated by
    /// cookie, also requires the <c>X-CSRF</c> header to protect cookie-based calls from CSRF.
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
