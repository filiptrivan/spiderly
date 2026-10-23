using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Spiderly.Security.Authentication;
using Spiderly.Security.Extensions;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Security.Tests
{
    /// <summary>
    /// Pins that <see cref="DefaultApiKeyAuthenticator{TApiKey}"/> judges a key's <c>ExpiresAt</c> by the app's
    /// registered <see cref="TimeProvider"/>, so a test can expire a key by advancing its clock. The authenticator is
    /// resolved through <c>AddSpiderlyApiKeyAuthentication</c>, as an app gets it, over SQLite.
    /// </summary>
    public class DefaultApiKeyAuthenticatorTests
    {
        private const string KeyHash = "hash-of-the-presented-key";

        [Fact]
        public async Task AKey_StopsAuthenticating_OnceTheRegisteredClockPassesItsExpiry()
        {
            using SqliteConnection connection = new("DataSource=:memory:");
            connection.Open();
            // Far from the machine's clock, so judging expiry by it can never pass by coincidence.
            DateTimeOffset issuedAt = new(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
            FakeTimeProvider clock = new(issuedAt);

            ServiceCollection services = new();
            services.AddSingleton<TimeProvider>(clock);
            services.AddDbContext<IApplicationDbContext, TestDbContext>(options => options.UseSqlite(connection));
            services.AddSpiderlyApiKeyAuthentication<TestApiKey>();
            using ServiceProvider provider = services.BuildServiceProvider();

            long keyId;
            using (IServiceScope scope = provider.CreateScope())
            {
                TestDbContext context = (TestDbContext)scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                context.Database.EnsureCreated();
                TestApiKey key = new() { KeyHash = KeyHash, ExpiresAt = issuedAt.UtcDateTime.AddHours(1) };
                context.Add(key);
                await context.SaveChangesAsync();
                keyId = key.Id;
            }

            Assert.Equal(keyId, await ResolveAsync(provider));

            clock.Advance(TimeSpan.FromHours(2));

            Assert.Null(await ResolveAsync(provider));
        }

        private static async Task<long?> ResolveAsync(ServiceProvider provider)
        {
            using IServiceScope scope = provider.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IApiKeyAuthenticator>().ResolveActiveApiKeyIdAsync(KeyHash);
        }

        private sealed class TestDbContext : DbContext, IApplicationDbContext
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();

            protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<TestApiKey>();
        }
    }
}
