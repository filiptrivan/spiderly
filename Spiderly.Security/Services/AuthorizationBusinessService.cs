using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Extensions;
using Spiderly.Security.Interfaces;
using Azure.Storage.Blobs;
using Spiderly.Security.Enums;
using Spiderly.Security.DTO;
using Microsoft.EntityFrameworkCore;

namespace Spiderly.Security.Services
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
