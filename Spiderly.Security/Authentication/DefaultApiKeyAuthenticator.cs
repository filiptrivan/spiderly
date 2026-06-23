using Microsoft.EntityFrameworkCore;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Authentication
{
    /// <summary>
    /// The framework's default <see cref="IApiKeyAuthenticator"/>: resolves a presented key's hash to the id of
    /// the active <typeparamref name="TApiKey"/> it identifies, querying the consumer's API-key table generically
    /// (the same pattern as <c>RolePermissionResolver&lt;TPrincipal&gt;</c>). Registered by
    /// <c>AddSpiderlyApiKeyAuthentication&lt;TApiKey&gt;</c>, so a consumer needs no hand-written authenticator —
    /// but can register its own <see cref="IApiKeyAuthenticator"/> beforehand to override. The revoke / expiry /
    /// disabled filters here are what make API keys individually revocable (a stateless token could not be).
    /// </summary>
    /// <typeparam name="TApiKey">The consumer's API-key entity.</typeparam>
    public class DefaultApiKeyAuthenticator<TApiKey> : IApiKeyAuthenticator
        where TApiKey : class, IApiKey
    {
        private readonly IApplicationDbContext _context;

        /// <summary>Creates the authenticator over the application DbContext.</summary>
        /// <param name="context">The application DbContext used to look the key up.</param>
        public DefaultApiKeyAuthenticator(IApplicationDbContext context)
        {
            _context = context;
        }

        /// <inheritdoc/>
        public async Task<long?> ResolveActiveApiKeyIdAsync(string keyHash)
        {
            return await _context.DbSet<TApiKey>()
                .AsNoTracking()
                .Where(x => x.KeyHash == keyHash
                    && x.IsRevoked != true
                    && x.IsDisabled != true
                    && (x.ExpiresAt == null || x.ExpiresAt > DateTime.UtcNow))
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();
        }
    }
}
