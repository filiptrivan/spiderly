using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Services;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;

namespace Spiderly.Security.Services
{
    public class AuthenticationService : BusinessServiceBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IApplicationDbContext _context;

        public AuthenticationService(
            IHttpContextAccessor httpContextAccessor, 
            IApplicationDbContext context
        )
            : base(context)
        {
            _httpContextAccessor = httpContextAccessor;
            _context = context;
        }
        
        public long GetCurrentUserId()
        {
            return Helper.GetCurrentUserId(_httpContextAccessor.HttpContext);
        }

        public string GetCurrentUserEmail()
        {
            return Helper.GetCurrentUserEmail(_httpContextAccessor.HttpContext);
        }

        public async Task<TUser> GetCurrentUser<TUser>() where TUser : class, IUser, new()
        {
            return await _context.WithTransactionAsync(async () =>
            {
                return await GetInstanceAsync<TUser, long>(GetCurrentUserId(), null);
            });
        }

        public async Task<string> GetAccessTokenAsync()
        {
            return await _httpContextAccessor.HttpContext.GetTokenAsync("Bearer", "access_token");
        }

        public string GetIPAddress()
        {
            return Helper.GetIPAddress(_httpContextAccessor.HttpContext);
        }
    }
}
