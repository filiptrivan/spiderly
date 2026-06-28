using System.Text.Json;
using Spiderly.Shared.IntegrationEvents;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="IntegrationEventOutboxHandler"/> dispatch — the event-specific half. Each outbox row
    /// targets ONE handler (harvest fans out one row per handler), so delivery resolves the row to the handler whose
    /// <see cref="IIntegrationEventHandler.Code"/> matches and runs only it. The code↔type machinery is the shared
    /// <see cref="CodeTypeRegistry{TMarker}"/> (covered by <see cref="OutboxCoreTests"/>).
    /// </summary>
    public class IntegrationEventTests
    {
        [Fact]
        public async Task Delivers_only_to_the_handler_matching_the_target_code()
        {
            CodeTypeRegistry<IIntegrationEvent> registry = NewRegistry();
            RecordingOrderHandler a = new("A");
            RecordingOrderHandler b = new("B");
            IntegrationEventOutboxHandler sut = NewHandler(registry, a, b);

            await sut.HandleAsync(
                BuildPayload(new OrderCreatedTestEvent { AggregateId = 42, Note = "hi" }, targetHandlerCode: "A"), default);

            OrderCreatedTestEvent handled = Assert.Single(a.Handled);
            Assert.Equal(42, handled.AggregateId);
            Assert.Equal("hi", handled.Note);
            Assert.Empty(b.Handled); // a sibling handler is its own row — untouched by this one
        }

        [Fact]
        public async Task Throws_when_no_handler_matches_the_target_code()
        {
            CodeTypeRegistry<IIntegrationEvent> registry = NewRegistry();
            IntegrationEventOutboxHandler sut = NewHandler(registry, new RecordingOrderHandler("A"));

            // Target a code with no live handler (removed/renamed since harvest) → fail so the row retries/dead-letters.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.HandleAsync(BuildPayload(new OrderCreatedTestEvent { AggregateId = 1 }, targetHandlerCode: "gone"), default));
        }

        [Fact]
        public async Task A_failing_targeted_handler_throws_so_the_row_retries()
        {
            CodeTypeRegistry<IIntegrationEvent> registry = NewRegistry();
            ThrowingOrderHandler failing = new();
            IntegrationEventOutboxHandler sut = NewHandler(registry, failing);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sut.HandleAsync(BuildPayload(new OrderCreatedTestEvent { AggregateId = 9 }, targetHandlerCode: failing.Code), default));
        }

        // ---- helpers ----

        private static CodeTypeRegistry<IIntegrationEvent> NewRegistry()
            => new(new[] { typeof(OrderCreatedTestEvent), typeof(OtherTestEvent) });

        private static IntegrationEventOutboxHandler NewHandler(
            CodeTypeRegistry<IIntegrationEvent> registry, params IIntegrationEventHandler[] handlers)
            => new(registry, handlers);

        // Builds the outbox payload exactly as harvest would: the event's envelope plus the target handler's Code.
        private static string BuildPayload(IIntegrationEvent integrationEvent, string targetHandlerCode)
        {
            OutboxEnvelope env = OutboxEnvelope.For(integrationEvent);
            return JsonSerializer.Serialize(new IntegrationEventOutboxPayload
            {
                Code = env.Code,
                Data = env.Data,
                TargetHandlerCode = targetHandlerCode,
            });
        }
    }

    [OutboxCode("OrderCreatedTest")]
    public class OrderCreatedTestEvent : IntegrationEvent
    {
        public string Note { get; set; }
    }

    [OutboxCode("OtherTest")]
    public class OtherTestEvent : IntegrationEvent { }

    public class RecordingOrderHandler : IntegrationEventHandler<OrderCreatedTestEvent>
    {
        private readonly string _code;
        public RecordingOrderHandler(string code = "RecordingOrderHandler") => _code = code;
        public override string Code => _code;
        public List<OrderCreatedTestEvent> Handled { get; } = new();

        protected override Task HandleAsync(OrderCreatedTestEvent integrationEvent, CancellationToken cancellationToken)
        {
            Handled.Add(integrationEvent);
            return Task.CompletedTask;
        }
    }

    public class ThrowingOrderHandler : IntegrationEventHandler<OrderCreatedTestEvent>
    {
        protected override Task HandleAsync(OrderCreatedTestEvent integrationEvent, CancellationToken cancellationToken)
            => throw new InvalidOperationException("handler boom");
    }
}
