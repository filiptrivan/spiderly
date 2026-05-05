using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Spiderly.Shared.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<TEntity> DbSet<TEntity>() where TEntity : class;

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken));

        DatabaseFacade Database { get; }

        /// <summary>
        /// Exposes the underlying EF Core <see cref="ChangeTracker"/> so callers (notably
        /// <c>WithTransactionAsync</c>) can detect pending tracked changes at commit time
        /// and surface a missing <c>SaveChangesAsync</c> as a loud failure rather than a
        /// silent dropped write.
        /// </summary>
        ChangeTracker ChangeTracker { get; }
    }
}
