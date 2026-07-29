using System.Text.Json;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Unit tests for the shared outbox-delivery core (`docs/outbox-core-unification.md`): the producer-side
    /// <see cref="OutboxCode.Of"/> code read, the <see cref="OutboxEnvelope"/> serialize, and the delivery-side
    /// <see cref="CodeTypeRegistry{TMarker}"/> (code↔type round-trip, rebuild, dup-code guard, unknown-code).
    /// </summary>
    public class OutboxCoreTests
    {
        [Fact]
        public void OutboxCode_Of_reads_the_attribute()
            => Assert.Equal("WidgetHappened", OutboxCode.Of(typeof(WidgetHappened)));

        [Fact]
        public void OutboxCode_Of_throws_when_missing()
            => Assert.Throws<InvalidOperationException>(() => OutboxCode.Of(typeof(Uncoded)));

        [Fact]
        public void Envelope_For_carries_code_and_serialized_data()
        {
            OutboxEnvelope env = OutboxEnvelope.For(new WidgetHappened { Size = 7 });

            Assert.Equal("WidgetHappened", env.Code);
            Assert.Contains("\"Size\":7", env.Data);
        }

        [Fact]
        public void Registry_round_trips_code_type_and_rebuild()
        {
            CodeTypeRegistry<ITestFact> registry = new(new[] { typeof(WidgetHappened) });
            Assert.Equal(typeof(WidgetHappened), registry.ResolveType("WidgetHappened"));

            OutboxEnvelope env = OutboxEnvelope.For(new WidgetHappened { Size = 9 });
            WidgetHappened rebuilt = Assert.IsType<WidgetHappened>(registry.Rebuild(env.Code, env.Data));
            Assert.Equal(9, rebuilt.Size);
        }

        [Fact]
        public void Registry_throws_on_duplicate_code()
            => Assert.Throws<InvalidOperationException>(() =>
                new CodeTypeRegistry<ITestFact>(new[] { typeof(WidgetHappened), typeof(WidgetHappenedTwin) }));

        [Fact]
        public void Registry_throws_for_unknown_code()
        {
            CodeTypeRegistry<ITestFact> registry = new(new[] { typeof(WidgetHappened) });
            Assert.Throws<InvalidOperationException>(() => registry.ResolveType("NoSuchCode"));
        }

        [Fact]
        public void NotificationOutboxPayload_round_trips_its_derived_fields_through_object_serialization()
        {
            // Notifier stages the payload via IOutbox.Enqueue(string, object) — STJ serializes the RUNTIME type, so the
            // derived RecipientId/ChannelCode are written even though the static type Enqueue sees is `object`. This guards
            // that load-bearing behavior: if Enqueue's param were ever narrowed to OutboxEnvelope (or a Serialize<OutboxEnvelope>
            // slipped in), the derived fields would be silently sliced off and every Outbox-delivery notification would lose its
            // recipient and channel — with no compile error and no other test failing. Lock the full round-trip + wire shape.
            object payload = new NotificationOutboxPayload
            {
                Code = "OrderConfirmed",
                Data = "{\"OrderId\":42}",
                RecipientId = 42,
                ChannelCode = "Email",
            };

            string json = JsonSerializer.Serialize(payload); // `object` root — mirrors Enqueue's signature
            Assert.Contains("\"RecipientId\":42", json);
            Assert.Contains("\"ChannelCode\":\"Email\"", json);

            NotificationOutboxPayload rebuilt = JsonSerializer.Deserialize<NotificationOutboxPayload>(json)!;
            Assert.Equal("OrderConfirmed", rebuilt.Code);
            Assert.Equal("{\"OrderId\":42}", rebuilt.Data);
            Assert.Equal(42, rebuilt.RecipientId);
            Assert.Equal("Email", rebuilt.ChannelCode);
        }
    }

    public interface ITestFact { }

    [OutboxCode("WidgetHappened")]
    public class WidgetHappened : ITestFact { public int Size { get; set; } }

    [OutboxCode("WidgetHappened")] // intentional duplicate code for the dup-guard test
    public class WidgetHappenedTwin : ITestFact { }

    public class Uncoded : ITestFact { }
}
