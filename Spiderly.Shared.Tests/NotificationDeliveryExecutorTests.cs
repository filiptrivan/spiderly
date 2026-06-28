using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="NotificationDeliveryExecutor"/> — the shared delivery core: rebuild the
    /// notification from code + JSON, find the channel by code, skip unconfigured channels, and reload the recipient
    /// via the resolver (throwing if one is needed but absent).
    /// </summary>
    public class NotificationDeliveryExecutorTests
    {
        [Fact]
        public async Task Delivers_rebuilt_notification_to_the_matching_channel()
        {
            RecordingChannel channel = new("Email");
            NotificationDeliveryExecutor executor = NewExecutor(channels: new[] { channel });

            await executor.DeliverAsync("TestNote", Serialize(new TestNotification()), recipientId: null, "Email", default);

            Assert.Equal(1, channel.SendCount);
            Assert.IsType<TestNotification>(channel.LastNotification);
            Assert.Null(channel.LastRecipient);
        }

        [Fact]
        public async Task Unconfigured_channel_is_skipped()
        {
            RecordingChannel channel = new("Email", configured: false);
            NotificationDeliveryExecutor executor = NewExecutor(channels: new[] { channel });

            await executor.DeliverAsync("TestNote", Serialize(new TestNotification()), recipientId: null, "Email", default);

            Assert.Equal(0, channel.SendCount);
        }

        [Fact]
        public async Task Unknown_channel_code_throws()
        {
            NotificationDeliveryExecutor executor = NewExecutor(channels: new[] { new RecordingChannel("Email") });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.DeliverAsync("TestNote", Serialize(new TestNotification()), recipientId: null, "Nope", default));
        }

        [Fact]
        public async Task Recipient_is_resolved_by_id_and_passed_to_the_channel()
        {
            RecordingChannel channel = new("Email");
            FakeRecipient recipient = new(42);
            NotificationDeliveryExecutor executor = NewExecutor(
                channels: new[] { channel },
                resolver: new FakeResolver(recipient));

            await executor.DeliverAsync("TestNote", Serialize(new TestNotification()), recipientId: 42, "Email", default);

            Assert.Same(recipient, channel.LastRecipient);
        }

        [Fact]
        public async Task Recipient_id_without_a_resolver_throws()
        {
            NotificationDeliveryExecutor executor = NewExecutor(channels: new[] { new RecordingChannel("Email") });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                executor.DeliverAsync("TestNote", Serialize(new TestNotification()), recipientId: 42, "Email", default));
        }

        // ---- helpers ----

        private static string Serialize(TestNotification n) => JsonSerializer.Serialize(n, typeof(TestNotification));

        private static NotificationDeliveryExecutor NewExecutor(
            INotificationChannel[] channels,
            INotificationRecipientResolver? resolver = null)
            => new(
                new CodeTypeRegistry<INotification>(new[] { typeof(TestNotification) }),
                channels,
                resolver == null ? Array.Empty<INotificationRecipientResolver>() : new[] { resolver },
                NullLogger<NotificationDeliveryExecutor>.Instance);

        [OutboxCode("TestNote")]
        private sealed class TestNotification : INotification
        {
        }

        private sealed class RecordingChannel : INotificationChannel
        {
            public RecordingChannel(string code, bool configured = true)
            {
                Code = code;
                IsConfigured = configured;
            }

            public string Code { get; }
            public bool IsConfigured { get; }
            public int SendCount { get; private set; }
            public INotification? LastNotification { get; private set; }
            public INotificationRecipient? LastRecipient { get; private set; }

            public Task SendAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken)
            {
                SendCount++;
                LastNotification = notification;
                LastRecipient = recipient;
                return Task.CompletedTask;
            }
        }

        private sealed class FakeRecipient : INotificationRecipient
        {
            public FakeRecipient(long id) => NotificationRecipientId = id;
            public long NotificationRecipientId { get; }
        }

        private sealed class FakeResolver : INotificationRecipientResolver
        {
            private readonly INotificationRecipient _recipient;
            public FakeResolver(INotificationRecipient recipient) => _recipient = recipient;
            public Task<INotificationRecipient> ResolveAsync(long recipientId) => Task.FromResult(_recipient);
        }
    }
}
