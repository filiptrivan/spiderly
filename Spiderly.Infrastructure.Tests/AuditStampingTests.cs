using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Spiderly.Security.Interfaces;
using Spiderly.Shared.BaseEntities;

namespace Spiderly.Infrastructure.Tests
{
    /// <summary>
    /// Behavioral coverage for <see cref="ApplicationDbContext{TUser}"/>'s audit stamping and
    /// optimistic-concurrency versioning — the framework's most load-bearing runtime behavior,
    /// previously pinned by nothing (every other test hand-rolls a bypass context). The fixture
    /// derives from the REAL context so the SaveChanges override is exercised, over SQLite so the
    /// concurrency-token WHERE clause and the CreatedAt IsModified pin are verified at the SQL level.
    /// </summary>
    public class AuditStampingTests
    {
        [Fact]
        public async Task Insert_with_explicit_historical_CreatedAt_is_preserved()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();
            DateTime historical = new(2020, 5, 5, 8, 30, 0, DateTimeKind.Utc);

            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity entity = new() { CreatedAt = historical };
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity reloaded = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal(historical, reloaded.CreatedAt);
            }
        }

        [Fact]
        public async Task Insert_with_default_CreatedAt_is_stamped_now()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();

            DateTime before = DateTime.UtcNow;
            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity entity = new();
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
            }
            DateTime after = DateTime.UtcNow;

            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity reloaded = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.InRange(reloaded.CreatedAt, before, after);
                Assert.InRange(reloaded.ModifiedAt, before, after);
                Assert.Equal(1, reloaded.Version);
            }
        }

        [Fact]
        public async Task Stamps_read_the_TimeProvider_registered_in_DI()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();
            // Far from the machine's clock, so stamping from it can never pass by coincidence.
            DateTime createdAt = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            FakeTimeProvider clock = new(new DateTimeOffset(createdAt));
            using ServiceProvider services = new ServiceCollection()
                .AddSingleton<TimeProvider>(clock)
                .AddDbContext<TestDbContext>(options => options.UseSqlite(connection))
                .BuildServiceProvider();

            long id;
            using (IServiceScope scope = services.CreateScope())
            {
                TestDbContext ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                ctx.Database.EnsureCreated();
                AuditEntity entity = new();
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
            }

            using (IServiceScope scope = services.CreateScope())
            {
                AuditEntity reloaded = await scope.ServiceProvider.GetRequiredService<TestDbContext>()
                    .Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal(createdAt, reloaded.CreatedAt);
                Assert.Equal(createdAt, reloaded.ModifiedAt);
            }

            clock.Advance(TimeSpan.FromHours(1));

            using (IServiceScope scope = services.CreateScope())
            {
                TestDbContext ctx = scope.ServiceProvider.GetRequiredService<TestDbContext>();
                AuditEntity entity = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                ctx.Entry(entity).State = EntityState.Modified;
                await ctx.SaveChangesAsync();
            }

            using (IServiceScope scope = services.CreateScope())
            {
                AuditEntity reloaded = await scope.ServiceProvider.GetRequiredService<TestDbContext>()
                    .Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal(createdAt, reloaded.CreatedAt);
                Assert.Equal(createdAt.AddHours(1), reloaded.ModifiedAt);
            }
        }

        [Fact]
        public async Task A_context_built_outside_the_container_stamps_the_clock_its_options_carry()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();
            DateTime createdAt = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            using ServiceProvider clockOnly = new ServiceCollection()
                .AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(createdAt)))
                .BuildServiceProvider();

            using TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>()
                .UseSqlite(connection)
                .UseApplicationServiceProvider(clockOnly)
                .Options);
            ctx.Database.EnsureCreated();
            AuditEntity entity = new();
            ctx.Set<AuditEntity>().Add(entity);
            await ctx.SaveChangesAsync();

            Assert.Equal(createdAt, entity.CreatedAt);
            Assert.Equal(createdAt, entity.ModifiedAt);
        }

        [Fact]
        public async Task Update_cannot_change_CreatedAt_and_bumps_ModifiedAt_and_Version()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();

            long id;
            DateTime stampedCreatedAt;
            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity entity = new();
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
                stampedCreatedAt = entity.CreatedAt;
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity entity = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                // A caller trying to move CreatedAt on update must be ignored (the Modified branch
                // pins it as not-modified); ModifiedAt/Version still advance.
                entity.CreatedAt = new DateTime(1999, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                await ctx.SaveChangesAsync();
            }

            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity reloaded = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal(stampedCreatedAt, reloaded.CreatedAt);
                Assert.True(reloaded.ModifiedAt >= stampedCreatedAt);
                Assert.Equal(2, reloaded.Version);
            }
        }

        [Fact]
        public async Task Insert_with_explicit_ModifiedAt_is_overwritten_with_now()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();
            DateTime historical = new(2020, 5, 5, 8, 30, 0, DateTimeKind.Utc);

            DateTime before = DateTime.UtcNow;
            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                // ModifiedAt is asymmetric to CreatedAt: it always means "last write in this system",
                // so a caller-supplied value must NOT be preserved on insert.
                AuditEntity entity = new() { CreatedAt = historical, ModifiedAt = historical };
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
            }
            DateTime after = DateTime.UtcNow;

            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity reloaded = await ctx.Set<AuditEntity>().SingleAsync(x => x.Id == id);
                Assert.Equal(historical, reloaded.CreatedAt);
                Assert.InRange(reloaded.ModifiedAt, before, after);
            }
        }

        [Fact]
        public async Task Concurrent_updates_lose_on_stale_Version()
        {
            using SqliteConnection connection = TestContexts.NewOpenConnection();

            long id;
            using (TestDbContext ctx = NewContext(connection))
            {
                AuditEntity entity = new();
                ctx.Set<AuditEntity>().Add(entity);
                await ctx.SaveChangesAsync();
                id = entity.Id;
            }

            // Two contexts load the same row at Version 1. The first save wins (→ Version 2);
            // the second saves against the now-stale Version 1 → 0 rows matched → concurrency throw.
            // This is the exact mechanism pa-cms's stock-decrement oversell protection rests on.
            using TestDbContext ctxA = NewContext(connection);
            using TestDbContext ctxB = NewContext(connection);

            AuditEntity a = await ctxA.Set<AuditEntity>().SingleAsync(x => x.Id == id);
            AuditEntity b = await ctxB.Set<AuditEntity>().SingleAsync(x => x.Id == id);

            a.ModifiedAt = a.ModifiedAt.AddMinutes(1);
            await ctxA.SaveChangesAsync();

            b.ModifiedAt = b.ModifiedAt.AddMinutes(2);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => ctxB.SaveChangesAsync());
        }

        private static TestDbContext NewContext(SqliteConnection connection)
        {
            TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private class AuditEntity : BusinessObject<long>
        {
        }

        private sealed class TestDbContext : ApplicationDbContext<TestUser>
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                // Map only the test entity — bypass the base's assembly-wide entity discovery and
                // relationship configuration, which need a full consumer model we don't have here.
                modelBuilder.Entity<AuditEntity>();
            }
        }

    }
}
