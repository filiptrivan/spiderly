using Spider.Shared.Interfaces;
using Spider.Shared.Extensions;
using Spider.Security.Interfaces;
using Azure.Storage.Blobs;
using Spider.Security.Enums;
using Spider.Security.DTO;
using Microsoft.EntityFrameworkCore;

namespace Spider.Security.Services
{
    public class AuthorizationBusinessService<TUser> : AuthorizationBusinessServiceGenerated<TUser> where TUser : class, IUser, new()
    {
        private readonly IApplicationDbContext _context;
        private readonly AuthenticationService _authenticationService;

        public AuthorizationBusinessService(IApplicationDbContext context, AuthenticationService authenticationService)
            : base(context, authenticationService)
        {
            _context = context;
            _authenticationService = authenticationService;
        }

        

    }
}
