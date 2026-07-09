using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Spiderly.Shared;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Behavior tests for <see cref="Notifier"/> — "no route → nothing" and per-channel fan-out for
    /// both delivery modes (<c>FireNow</c> → a Hangfire job per channel; <c>Outbox</c> → an outbox row per channel).
    /// </summary>
    public class NotifierTests
    {
        [Fact]
        public void Outbox_notification_stages_one_outbox_row_per_channel()
        {
            FakeOutbox outbox = new();
            Notifier notifier = NewNotifier(outbox, new FakeBackgroundJobClient(), "Email", "Telegram");

            notifier.NotifyAdmins(new TestNotification { DeliveryMode = NotificationDelivery.Outbox });

            Assert.Equal(2, outbox.Enqueued.Count);
            Assert.All(outbox.Enqueued, e => Assert.Equal(NotificationOutboxHandler.HandlerCode, e.HandlerCode));
            Assert.Equal(
                new[] { "Email", "Telegram" },
                outbox.Enqueued.Select(e => ((NotificationOutboxPayload)e.Payload).ChannelCode));
        }

        [Fact]
        public void FireNow_notification_enqueues_one_hangfire_job_per_channel()
        {
            FakeBackgroundJobClient jobs = new();
            Notifier notifier = NewNotifier(new FakeOutbox(), jobs, "Email", "Telegram");

            notifier.NotifyAdmins(new TestNotification { DeliveryMode = NotificationDelivery.FireNow });

            Assert.Equal(2, jobs.Created.Count);
            Assert.All(jobs.Created, j => Assert.Equal(nameof(NotificationDeliveryJob.DeliverAsync), j.Method.Name));
            Assert.Equal(
                new[] { "Email", "Telegram" },
                jobs.Created.Select(j => (string)j.Args[3])); // arg[3] = channelCode
        }

        [Fact]
        public void No_route_sends_nothing()
        {
            FakeOutbox outbox = new();
            FakeBackgroundJobClient jobs = new();
            Notifier notifier = NewNotifier(outbox, jobs /* no channels routed */);

            notifier.NotifyAdmins(new TestNotification { DeliveryMode = NotificationDelivery.Outbox });

            Assert.Empty(outbox.Enqueued);
            Assert.Empty(jobs.Created);
        }

        // ---- helpers ----

        private static Notifier NewNotifier(FakeOutbox outbox, FakeBackgroundJobClient jobClient, params string[] routedChannelCodes)
        {
            FakeRouter router = new(routedChannelCodes.Select(c => (INotificationChannel)new StubChannel(c)).ToList());
            return new Notifier(router, jobClient, new IOutbox[] { outbox });
        }

        [OutboxCode("TestNote")]
        private sealed class TestNotification : INotification
        {
            public NotificationDelivery DeliveryMode { get; set; } = NotificationDelivery.FireNow;
            public NotificationDelivery Delivery => DeliveryMode;
        }

        private sealed class StubChannel : INotificationChannel
        {
            public StubChannel(string code) => Code = code;
            public string Code { get; }
            public bool IsConfigured => true;
            public Task SendAsync(INotification n, INotificationRecipient r, CancellationToken ct) => Task.CompletedTask;
        }

        private sealed class FakeRouter : INotificationRouter
        {
            private readonly IReadOnlyCollection<INotificationChannel> _channels;
            public FakeRouter(IReadOnlyCollection<INotificationChannel> channels) => _channels = channels;
            public IReadOnlyCollection<INotificationChannel> ChannelsFor(INotification notification) => _channels;
        }

        private sealed class FakeOutbox : IOutbox
        {
            public List<(string HandlerCode, object Payload)> Enqueued { get; } = new();
            public void Enqueue(string handlerCode, object payload) => Enqueued.Add((handlerCode, payload));
        }

        private sealed class FakeBackgroundJobClient : IBackgroundJobClient
        {
            public List<Job> Created { get; } = new();
            public string Create(Job job, IState state)
            {
                Created.Add(job);
                return Guid.NewGuid().ToString();
            }
            public bool ChangeState(string jobId, IState state, string expectedState) => true;
        }
    }
}
