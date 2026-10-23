using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Tests
{
    // AddSpiderly as an app calls it, with empty configuration and an unconnected PostgreSQL context, for tests whose
    // claim is the registration set. Nothing here opens a connection: the context's options run only when it is resolved.
    internal static class SpiderlyTestApp
    {
        public static SpiderlyBuilder AddSpiderly(IServiceCollection services, Action<SpiderlyBuilder> configure)
            => services.AddSpiderly<AppDbContext>(new ConfigurationBuilder().Build(), spiderly =>
            {
                spiderly.UsePostgreSQL();
                configure(spiderly);
            });

        public sealed class AppDbContext : DbContext, IApplicationDbContext
        {
            public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();
        }
    }
}
