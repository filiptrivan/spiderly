using Hangfire;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Spiderly.Shared.Attributes;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Interfaces;
using Spiderly.Shared.Notifications;
using Spiderly.Shared.Outbox;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Every job Spiderly schedules itself is registered by the AddSpiderly feature that schedules it, so an app whose
    /// activator builds only registered jobs (<c>spiderly.AddJobs(...)</c>) runs them, and a host that validates its graph
    /// at boot judges them. The jobs are read off what Spiderly actually hands Hangfire; the container is asked whether it
    /// knows each, not made to build it (that needs a database).
    /// </summary>
    public class SpiderlyScheduledJobsTests
    {
        [Fact]
        public void Every_job_Spiderly_schedules_is_registered_by_AddSpiderly()
        {
            ScheduledJobs jobs = ScheduleWhatSpiderlySchedules(_ => { });

            Assert.Equal(
                new[]
                {
                    typeof(OutboxDispatcherJob<TestOutboxMessage>),
                    typeof(OutboxRetentionJob<TestOutboxMessage>),
                    typeof(OutboxHealthJob<TestOutboxMessage>),
                    typeof(NotificationDeliveryJob),
                },
                jobs.Scheduled);
            Assert.Empty(jobs.Unregistered);
        }

        [Fact]
        public void NegativeControl_a_scheduled_job_the_container_does_not_know_is_reported()
        {
            ScheduledJobs jobs = ScheduleWhatSpiderlySchedules(
                services => services.RemoveAll<OutboxDispatcherJob<TestOutboxMessage>>());

            Assert.Contains(typeof(OutboxDispatcherJob<TestOutboxMessage>), jobs.Unregistered);
        }

        private sealed record ScheduledJobs(IReadOnlyList<Type> Scheduled, IReadOnlyList<Type> Unregistered);

        /// <summary>
        /// Runs every Spiderly call that schedules a job against an app with the outbox and notifications on, and
        /// returns the job types they handed Hangfire and those of them the container does not know.
        /// </summary>
        private static ScheduledJobs ScheduleWhatSpiderlySchedules(Action<IServiceCollection> afterAddSpiderly)
        {
            RecordingRecurringJobManager recurring = new();
            RecordingJobClient enqueued = new();
            ServiceCollection services = new();
            SpiderlyTestApp.AddSpiderly(services, spiderly => spiderly
                .AddOutbox<TestOutboxMessage>()
                .AddNotifications(routes => routes.Route<TestNotification>().To(StubChannel.ChannelCode)));
            services.AddSingleton<IRecurringJobManager>(recurring);
            afterAddSpiderly(services);
            using ServiceProvider provider = services.BuildServiceProvider();

            new ApplicationBuilder(provider).SpiderlyUseOutboxRecurringJob<TestOutboxMessage>();
            new Notifier(new StubRouter(), enqueued, Array.Empty<IOutbox>()).NotifyAdmins(new TestNotification());

            Type[] scheduled = recurring.Added.Select(a => a.Job.Type)
                .Concat(enqueued.Created.Select(c => c.Job.Type))
                .ToArray();
            IServiceProviderIsService container = provider.GetRequiredService<IServiceProviderIsService>();
            return new ScheduledJobs(scheduled, scheduled.Where(job => !container.IsService(job)).ToArray());
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

        [OutboxCode("ScheduledJobsTestNote")]
        private sealed class TestNotification : INotification
        {
            public NotificationDelivery Delivery => NotificationDelivery.FireNow;
        }

        private sealed class StubChannel : INotificationChannel
        {
            public const string ChannelCode = "Stub";
            public string Code => ChannelCode;
            public bool IsConfigured => true;
            public Task SendAsync(INotification notification, INotificationRecipient? recipient, CancellationToken cancellationToken)
                => Task.CompletedTask;
        }

        private sealed class StubRouter : INotificationRouter
        {
            public IReadOnlyCollection<INotificationChannel> ChannelsFor(INotification notification) => new[] { new StubChannel() };
        }
    }
}
