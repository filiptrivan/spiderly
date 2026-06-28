using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.IntegrationEvents;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// EF-backed integration tests for the harvest + dispatch loop (the EF <c>SaveChanges</c> mechanism the unit tests
    /// can't cover): raising an event on a tracked aggregate and calling <c>SaveChanges</c> must stage exactly one outbox
    /// row via <see cref="IntegrationEventOutboxInterceptor{TOutbox}"/> (an <see cref="OutboxEnvelope"/> with
    /// <c>AggregateId</c> stamped from the now-assigned id), and feeding that row to
    /// <see cref="IntegrationEventOutboxHandler"/> must invoke the matching handler. The interceptor reads
    /// <c>[OutboxCode]</c> off the type directly — no registry on the producer side.
    /// </summary>
    public class IntegrationEventInterceptorTests
    {
        [Fact]
        public async Task SaveChanges_harvests_a_raised_event_into_one_outbox_row_with_stamped_aggregate_id()
        {
            using TestDbContext ctx = NewContext();

            TestWidget widget = new();
            widget.RaiseIntegrationEvent(new WidgetCreated());
            ctx.Set<TestWidget>().Add(widget);
            await ctx.SaveChangesAsync();

            TestOutboxMessage row = Assert.Single(ctx.Set<TestOutboxMessage>());
            Assert.Equal(IntegrationEventOutboxHandler.HandlerCode, row.HandlerCode);

            IntegrationEventOutboxPayload envelope = JsonSerializer.Deserialize<IntegrationEventOutboxPayload>(row.Payload);
            Assert.Equal("WidgetCreated", envelope.Code);
            Assert.Equal("RecordingWidgetHandler", envelope.TargetHandlerCode); // fanned out to the one handler

            // The event is rebuilt from the envelope and its AggregateId is the entity's now-assigned id — i.e. the
            // interceptor harvested AFTER the id was generated (the whole reason it runs in SavedChangesAsync).
            WidgetCreated rebuilt = JsonSerializer.Deserialize<WidgetCreated>(envelope.Data);
            Assert.NotEqual(0, widget.Id);
            Assert.Equal(widget.Id, rebuilt.AggregateId);
        }

        [Fact]
        public async Task End_to_end_raise_then_save_then_dispatch_invokes_the_handler()
        {
            using TestDbContext ctx = NewContext();

            TestWidget widget = new();
            widget.RaiseIntegrationEvent(new WidgetCreated());
            ctx.Set<TestWidget>().Add(widget);
            await ctx.SaveChangesAsync();

            TestOutboxMessage row = Assert.Single(ctx.Set<TestOutboxMessage>());

            // Deliver the staged row exactly as the outbox sweep would.
            CodeTypeRegistry<IIntegrationEvent> registry = new(new[] { typeof(WidgetCreated) });
            RecordingWidgetHandler handler = new();
            IntegrationEventOutboxHandler dispatcher = new(registry, new IIntegrationEventHandler[] { handler });
            await dispatcher.HandleAsync(row.Payload, default);

            WidgetCreated received = Assert.Single(handler.Handled);
            Assert.Equal(widget.Id, received.AggregateId);
        }

        [Fact]
        public async Task An_entity_that_raised_nothing_stages_no_outbox_row()
        {
            using TestDbContext ctx = NewContext();

            ctx.Set<TestWidget>().Add(new TestWidget()); // no event raised
            await ctx.SaveChangesAsync();

            Assert.Empty(ctx.Set<TestOutboxMessage>());
        }

        [Fact]
        public async Task Each_raised_event_stages_its_own_row()
        {
            using TestDbContext ctx = NewContext();

            TestWidget a = new();
            TestWidget b = new();
            a.RaiseIntegrationEvent(new WidgetCreated());
            b.RaiseIntegrationEvent(new WidgetCreated());
            ctx.Set<TestWidget>().AddRange(a, b);
            await ctx.SaveChangesAsync();

            Assert.Equal(2, ctx.Set<TestOutboxMessage>().Count());
        }

        [Fact]
        public async Task A_raised_event_not_passed_to_AddIntegrationEvents_fails_fast_at_save()
        {
            using TestDbContext ctx = NewContext();

            // WidgetUnregistered carries [OutboxCode] but is NOT in the registry NewContext() built — i.e. the consumer
            // forgot to call AddIntegrationEvents(typeof(WidgetUnregistered)). It stages fine (the producer reads the
            // attribute) but would dead-letter at delivery; the preflight turns that into a loud failure at the raise site.
            TestWidget widget = new();
            widget.RaiseIntegrationEvent(new WidgetUnregistered());
            ctx.Set<TestWidget>().Add(widget);

            InvalidOperationException ex =
                await Assert.ThrowsAsync<InvalidOperationException>(() => ctx.SaveChangesAsync());
            Assert.Contains("AddIntegrationEvents", ex.Message);
        }

        // ---- harness ----

        private static TestDbContext NewContext()
        {
            // The interceptor reads [OutboxCode] off the type to STAGE; the registry is a producer-side preflight that the
            // raised event was registered (so an unregistered one fails fast here instead of dead-lettering at delivery).
            CodeTypeRegistry<IIntegrationEvent> registry = new(new[] { typeof(WidgetCreated) });
            // Harvest now fans out one row per registered handler, so it needs the handler set — supplied via a scope.
            IServiceScopeFactory scopeFactory = new FakeScopeFactory(new RecordingWidgetHandler());
            IntegrationEventOutboxInterceptor<TestOutboxMessage> interceptor =
                new(NullLogger<IntegrationEventOutboxInterceptor<TestOutboxMessage>>.Instance, registry, scopeFactory);

            // In-memory provider: non-relational, so the interceptor's ambient-transaction guard is skipped (IsRelational()
            // is false) and these saves don't need a WithTransactionAsync wrapper.
            return new TestDbContext(new DbContextOptionsBuilder<TestDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .AddInterceptors(interceptor)
                .Options);
        }

        [OutboxCode("WidgetCreated")]
        private sealed class WidgetCreated : IntegrationEvent { }

        [OutboxCode("WidgetUnregistered")]
        private sealed class WidgetUnregistered : IntegrationEvent { }

        private sealed class TestWidget : BusinessObject<long> { }

        private sealed class RecordingWidgetHandler : IntegrationEventHandler<WidgetCreated>
        {
            public List<WidgetCreated> Handled { get; } = new();

            protected override Task HandleAsync(WidgetCreated integrationEvent, CancellationToken cancellationToken)
            {
                Handled.Add(integrationEvent);
                return Task.CompletedTask;
            }
        }

        // Hands the interceptor's handler-map build its handler set (what GetServices<IIntegrationEventHandler>() asks for).
        private sealed class FakeScopeFactory : IServiceScopeFactory, IServiceScope, IServiceProvider
        {
            private readonly IEnumerable<IIntegrationEventHandler> _handlers;
            public FakeScopeFactory(params IIntegrationEventHandler[] handlers) => _handlers = handlers;
            public IServiceScope CreateScope() => this;
            public IServiceProvider ServiceProvider => this;
            public void Dispose() { }
            public object GetService(Type serviceType)
                => serviceType == typeof(IEnumerable<IIntegrationEventHandler>) ? _handlers : null;
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
            public DateTime? NextAttemptAt { get; set; }
            public long? DismissedByUserId { get; set; }
        }

        private sealed class TestDbContext : DbContext, IApplicationDbContext
        {
            public TestDbContext(DbContextOptions options) : base(options) { }

            public DbSet<TestWidget> Widgets => Set<TestWidget>();
            public DbSet<TestOutboxMessage> OutboxMessages => Set<TestOutboxMessage>();

            public DbSet<TEntity> DbSet<TEntity>() where TEntity : class => Set<TEntity>();
        }
    }
}
