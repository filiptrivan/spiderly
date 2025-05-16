using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Microsoft.AspNetCore.Authentication;
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

        public async override void OnActionExecuting(ActionExecutingContext context)
        {
            string accessToken = await context.HttpContext.GetTokenAsync("Bearer", "access_token");

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