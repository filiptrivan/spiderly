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
        private readonly TimeProvider _timeProvider;

        /// <summary>Creates the authenticator over the application DbContext.</summary>
        /// <param name="context">The application DbContext used to look the key up.</param>
        /// <param name="timeProvider">The app's clock, which a key's <see cref="IApiKey.ExpiresAt"/> is judged by.</param>
        public DefaultApiKeyAuthenticator(IApplicationDbContext context, TimeProvider timeProvider)
        {
            _context = context;
            _timeProvider = timeProvider;
        }

        /// <inheritdoc/>
        public async Task<long?> ResolveActiveApiKeyIdAsync(string keyHash)
        {
            DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

            return await _context.DbSet<TApiKey>()
                .AsNoTracking()
                .Where(x => x.KeyHash == keyHash
                    && x.IsRevoked != true
                    && x.IsDisabled != true
                    && (x.ExpiresAt == null || x.ExpiresAt > now))
                .Select(x => (long?)x.Id)
                .FirstOrDefaultAsync();
        }
    }
}
