using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for the framework outbox-ops jobs: <see cref="OutboxRetentionJob{TOutbox}"/> (purge handled rows,
    /// keep pending + dead-lettered) and <see cref="OutboxHealthJob{TOutbox}"/> (backlog-age + dead-letter alerting).
    /// Backed by a relational SQLite in-memory DB so <c>ExecuteDeleteAsync</c> and date comparisons run for real.
    /// </summary>
    public class OutboxOpsJobsTests : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly TestDbContext _ctx;

        public OutboxOpsJobsTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            _ctx = new TestDbContext(new DbContextOptionsBuilder<TestDbContext>().UseSqlite(_connection).Options);
            _ctx.Database.EnsureCreated();
        }

        public void Dispose()
        {
            _ctx.Dispose();
            _connection.Dispose();
        }

        [Fact]
        public async Task Retention_purges_only_handled_rows_past_the_window()
        {
            DateTime now = DateTime.UtcNow;
            Add(dispatchedAt: now.AddDays(-40));                                   // handled + old → purged
            Add(dispatchedAt: now.AddDays(-5));                                    // handled + recent → kept
            Add(dispatchedAt: null);                                              // pending → kept
            Add(dispatchedAt: null, nextAttemptAt: OutboxRetryPolicy.NeverRetry); // dead-lettered → kept
            await _ctx.SaveChangesAsync();

            await new OutboxRetentionJob<TestOutboxMessage>(_ctx, Opts(retentionDays: 30),
                NullLogger<OutboxRetentionJob<TestOutboxMessage>>.Instance).PurgeAsync();

            List<TestOutboxMessage> remaining = await _ctx.Set<TestOutboxMessage>().AsNoTracking().ToListAsync();
            Assert.Equal(3, remaining.Count);
            Assert.DoesNotContain(remaining, r => r.DispatchedAt != null && r.DispatchedAt < now.AddDays(-30));
        }

        [Fact]
        public async Task Health_alerts_on_backlog_age_and_counts_dead_letters()
        {
            DateTime now = DateTime.UtcNow;
            Add(dispatchedAt: null, createdAt: now.AddMinutes(-30));               // old due pending → age breach
            Add(dispatchedAt: null, nextAttemptAt: OutboxRetryPolicy.NeverRetry); // dead-lettered (future NextAttemptAt)
            await _ctx.SaveChangesAsync();

            OutboxHealth h = await new OutboxHealthJob<TestOutboxMessage>(_ctx, Opts(backlogAgeAlertMinutes: 15),
                NullLogger<OutboxHealthJob<TestOutboxMessage>>.Instance).ComputeHealthAsync();

            Assert.True(h.Alert);
            Assert.Equal(1, h.DeadLetters);
            Assert.True(h.OldestDueMinutes >= 29); // ~30; dead-letter excluded from "due"
        }

        [Fact]
        public async Task Health_is_quiet_when_backlog_young_and_no_dead_letters()
        {
            DateTime now = DateTime.UtcNow;
            Add(dispatchedAt: null, createdAt: now.AddMinutes(-2)); // young pending
            Add(dispatchedAt: now);                                 // handled
            await _ctx.SaveChangesAsync();

            OutboxHealth h = await new OutboxHealthJob<TestOutboxMessage>(_ctx, Opts(backlogAgeAlertMinutes: 15),
                NullLogger<OutboxHealthJob<TestOutboxMessage>>.Instance).ComputeHealthAsync();

            Assert.False(h.Alert);
            Assert.Equal(0, h.DeadLetters);
        }

        // ---- helpers ----

        private static IOptions<OutboxOptions> Opts(int retentionDays = 30, int backlogAgeAlertMinutes = 15)
            => Options.Create(new OutboxOptions { RetentionDays = retentionDays, BacklogAgeAlertMinutes = backlogAgeAlertMinutes });

        private void Add(DateTime? dispatchedAt, DateTime? nextAttemptAt = null, DateTime? createdAt = null)
            => _ctx.Set<TestOutboxMessage>().Add(new TestOutboxMessage
            {
                HandlerCode = "X",
                Payload = "{}",
                CreatedAt = createdAt ?? DateTime.UtcNow,
                DispatchedAt = dispatchedAt,
                NextAttemptAt = nextAttemptAt,
            });

        private sealed class TestOutboxMessage : IOutboxMessage
        {
            public long Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public string HandlerCode { get; set; } = "";
            public string Payload { get; set; } = "";
            public DateTime? DispatchedAt { get; set; }
            public int AttemptCount { get; set; }
            public DateTime? LastAttemptedAt { get; set; }
            public string LastError { get; set; } = "";
            public DateTime? NextAttemptAt { get; set; }
            public long? DismissedByUserId { get; set; }
        }

        private sealed class TestDbContext : DbContext, IApplicationDbContext
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            public DbSet<TestOutboxMessage> OutboxMessages => Set<TestOutboxMessage>();

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();
        }
    }
}
