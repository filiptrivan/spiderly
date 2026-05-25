using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="OutboxDispatcherJob{TOutbox}"/> — the generic transactional-outbox sweep:
    /// dispatch-by-handler-code, per-row failure isolation, missing-handler handling, and the loud
    /// duplicate-code guard. Backed by an EF Core in-memory database and fake handlers; no Hangfire involved
    /// (the job's <c>ProcessAsync</c> is called directly).
    /// </summary>
    public class OutboxDispatcherJobTests
    {
        [Fact]
        public async Task Dispatches_pending_row_to_matching_handler()
        {
            using TestDbContext ctx = NewContext();
            TestOutboxMessage row = await SeedAsync(ctx, handlerCode: "Email", payload: "{\"id\":1}");
            RecordingHandler handler = new("Email");

            await NewJob(ctx, handler).ProcessAsync();

            Assert.Equal(new[] { "{\"id\":1}" }, handler.ReceivedPayloads);
            Assert.NotNull(row.DispatchedAt);
        }

        [Fact]
        public async Task Handler_throws_so_row_stays_pending_and_records_error()
        {
            using TestDbContext ctx = NewContext();
            TestOutboxMessage row = await SeedAsync(ctx, handlerCode: "Email");

            await NewJob(ctx, new ThrowingHandler("Email")).ProcessAsync();

            Assert.Null(row.DispatchedAt);
            Assert.Equal(1, row.AttemptCount);
            Assert.NotNull(row.LastAttemptedAt);
            Assert.Contains("boom", row.LastError);
        }

        [Fact]
        public async Task Unknown_handler_code_is_not_dispatched_and_records_error()
        {
            using TestDbContext ctx = NewContext();
            TestOutboxMessage row = await SeedAsync(ctx, handlerCode: "Missing");

            await NewJob(ctx /* no handlers registered */).ProcessAsync();

            Assert.Null(row.DispatchedAt);
            Assert.Equal(1, row.AttemptCount);
            Assert.Contains("Missing", row.LastError);
        }

        [Fact]
        public async Task Duplicate_handler_codes_fail_loud()
        {
            using TestDbContext ctx = NewContext();
            await SeedAsync(ctx, handlerCode: "Email");

            OutboxDispatcherJob<TestOutboxMessage> job =
                NewJob(ctx, new RecordingHandler("Email"), new ThrowingHandler("Email"));

            await Assert.ThrowsAsync<InvalidOperationException>(() => job.ProcessAsync());
        }

        [Fact]
        public async Task Already_dispatched_rows_are_skipped()
        {
            using TestDbContext ctx = NewContext();
            await SeedAsync(ctx, handlerCode: "Email", dispatchedAt: DateTime.UtcNow);
            RecordingHandler handler = new("Email");

            await NewJob(ctx, handler).ProcessAsync();

            Assert.Empty(handler.ReceivedPayloads);
        }

        [Fact]
        public async Task Rows_at_the_retry_cap_are_skipped()
        {
            using TestDbContext ctx = NewContext();
            await SeedAsync(ctx, handlerCode: "Email", attemptCount: 10); // MaxAttempts = 10
            RecordingHandler handler = new("Email");

            await NewJob(ctx, handler).ProcessAsync();

            Assert.Empty(handler.ReceivedPayloads);
        }

        // ---- helpers ----

        private static TestDbContext NewContext()
            => new(new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);

        private static OutboxDispatcherJob<TestOutboxMessage> NewJob(
            IApplicationDbContext ctx, params IOutboxHandler[] handlers)
            => new(ctx, new FakeScopeFactory(handlers), NullLogger<OutboxDispatcherJob<TestOutboxMessage>>.Instance);

        // The dispatcher resolves handlers from a fresh DI scope per row; this minimal fake hands back the test's
        // handlers when asked for IEnumerable<IOutboxHandler> (what GetServices<IOutboxHandler>() requests).
        private sealed class FakeScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
        {
            private readonly IEnumerable<IOutboxHandler> _handlers;
            public FakeScopeFactory(IEnumerable<IOutboxHandler> handlers) => _handlers = handlers;

            public IServiceScope CreateScope() => this;
            public IServiceProvider ServiceProvider => this;
            public void Dispose() { }

            public object? GetService(Type serviceType)
                => serviceType == typeof(IEnumerable<IOutboxHandler>) ? _handlers : null;
        }

        private static async Task<TestOutboxMessage> SeedAsync(
            TestDbContext ctx,
            string handlerCode,
            string payload = "{}",
            DateTime? dispatchedAt = null,
            int attemptCount = 0)
        {
            TestOutboxMessage row = new()
            {
                HandlerCode = handlerCode,
                Payload = payload,
                CreatedAt = DateTime.UtcNow,
                DispatchedAt = dispatchedAt,
                AttemptCount = attemptCount,
            };
            ctx.OutboxMessages.Add(row);
            await ctx.SaveChangesAsync();
            return row;
        }

        private sealed class RecordingHandler : IOutboxHandler
        {
            public RecordingHandler(string code) => Code = code;
            public string Code { get; }
            public List<string> ReceivedPayloads { get; } = new();

            public Task HandleAsync(string payload, CancellationToken cancellationToken)
            {
                ReceivedPayloads.Add(payload);
                return Task.CompletedTask;
            }
        }

        private sealed class ThrowingHandler : IOutboxHandler
        {
            public ThrowingHandler(string code) => Code = code;
            public string Code { get; }

            public Task HandleAsync(string payload, CancellationToken cancellationToken)
                => throw new InvalidOperationException("boom");
        }

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
