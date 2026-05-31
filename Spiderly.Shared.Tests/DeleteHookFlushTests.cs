using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Characterizes the runtime contract the generated <c>Delete{Entity}</c>/<c>Delete{Entity}List</c>
    /// rely on: a tracked write staged by an <c>OnBefore...Delete</c> hook (e.g. <c>IOutbox.Enqueue</c>)
    /// is flushed inside the ambient <see cref="ApplicationDbContextExtensions.WithTransactionAsync"/>
    /// before the untracked <c>ExecuteDeleteAsync</c> cascade runs. Without the flush the
    /// clean-tracker-at-commit guard throws; with it, the staged write commits — or rolls back —
    /// atomically with the delete.
    ///
    /// Backed by SQLite in-memory (real transactions): EF InMemory can't model commit/rollback or the
    /// transaction the guard depends on. This pins the <i>pattern</i>; that the generator actually emits
    /// the flush is pinned separately by the source-generator snapshot tests.
    /// </summary>
    public class DeleteHookFlushTests
    {
        [Fact]
        public async Task Hook_stages_tracked_write_without_flush_trips_the_clean_tracker_guard()
        {
            using SqliteConnection connection = NewOpenConnection();
            await SeedWidgetAsync(connection, widgetId: 1);

            using TestDbContext ctx = NewContext(connection);

            // No flush after the hook -> the staged OutboxRow Add survives to commit and the guard fires.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                RunGeneratedDeleteShape(ctx, widgetId: 1, flush: false, onBeforeDelete: () => StageOutboxRow(ctx)));
        }

        [Fact]
        public async Task Hook_stages_tracked_write_with_flush_persists_atomically()
        {
            using SqliteConnection connection = NewOpenConnection();
            await SeedWidgetAsync(connection, widgetId: 1);

            using (TestDbContext ctx = NewContext(connection))
            {
                await RunGeneratedDeleteShape(ctx, widgetId: 1, flush: true, onBeforeDelete: () => StageOutboxRow(ctx));

                Assert.False(ctx.ChangeTracker.HasChanges()); // the unit of work flushed
            }

            // Read through a fresh context over the same in-memory db — the closest thing to a real round-trip.
            using TestDbContext verify = NewContext(connection);
            Assert.Equal(1, await verify.Set<OutboxRow>().CountAsync()); // staged write committed
            Assert.Equal(0, await verify.Set<Widget>().CountAsync());    // delete committed
        }

        [Fact]
        public async Task Failure_after_flush_rolls_back_the_staged_write_with_the_delete()
        {
            using SqliteConnection connection = NewOpenConnection();
            await SeedWidgetAsync(connection, widgetId: 1);

            using (TestDbContext ctx = NewContext(connection))
            {
                await Assert.ThrowsAsync<BoomException>(() =>
                    ctx.WithTransactionAsync(async () =>
                    {
                        StageOutboxRow(ctx);
                        await ctx.SaveChangesAsync();                              // flush inside the transaction
                        await ctx.Set<Widget>().Where(x => x.Id == 1).ExecuteDeleteAsync();
                        throw new BoomException();                                 // a later cascade/parent delete fails
                    }));
            }

            using TestDbContext verify = NewContext(connection);
            Assert.Equal(0, await verify.Set<OutboxRow>().CountAsync()); // staged write rolled back
            Assert.Equal(1, await verify.Set<Widget>().CountAsync());    // delete rolled back
        }

        // The shape the generated Delete{Entity} emits: hook -> (flush staged writes) -> untracked ExecuteDelete.
        private static Task RunGeneratedDeleteShape(TestDbContext ctx, long widgetId, bool flush, Func<Task> onBeforeDelete)
            => ctx.WithTransactionAsync(async () =>
            {
                await onBeforeDelete();

                if (flush && ctx.ChangeTracker.HasChanges())
                    await ctx.SaveChangesAsync();

                await ctx.Set<Widget>().Where(x => x.Id == widgetId).ExecuteDeleteAsync();
            });

        private static Task StageOutboxRow(TestDbContext ctx)
        {
            ctx.Set<OutboxRow>().Add(new OutboxRow { Payload = "{}" });
            return Task.CompletedTask;
        }

        private static SqliteConnection NewOpenConnection()
        {
            SqliteConnection connection = new("DataSource=:memory:");
            connection.Open(); // the in-memory database lives only while at least one connection is open
            return connection;
        }

        private static TestDbContext NewContext(SqliteConnection connection)
        {
            TestDbContext ctx = new(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(connection).Options);
            ctx.Database.EnsureCreated();
            return ctx;
        }

        private static async Task SeedWidgetAsync(SqliteConnection connection, long widgetId)
        {
            using TestDbContext ctx = NewContext(connection);
            ctx.Set<Widget>().Add(new Widget { Id = widgetId });
            await ctx.SaveChangesAsync();
        }

        private sealed class BoomException : Exception { }

        private class Widget
        {
            public long Id { get; set; }
        }

        private class OutboxRow
        {
            public long Id { get; set; }
            public string Payload { get; set; } = "";
        }

        private sealed class TestDbContext : DbContext, IApplicationDbContext
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            public DbSet<Widget> Widgets => Set<Widget>();
            public DbSet<OutboxRow> OutboxRows => Set<OutboxRow>();

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();
        }
    }
}
