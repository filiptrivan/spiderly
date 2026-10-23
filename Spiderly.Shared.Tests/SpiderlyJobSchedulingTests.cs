using System.Reflection;
using Hangfire;
using Hangfire.States;
using Spiderly.Shared.Jobs;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// The typed scheduling methods hand Hangfire a job on the marked type, as Hangfire's own generic methods would, and
    /// accept only a type that carries <see cref="ISpiderlyJob"/>.
    /// </summary>
    public class SpiderlyJobSchedulingTests
    {
        [Fact]
        public void EnqueueJob_enqueues_the_marked_type_s_method_with_its_arguments()
        {
            RecordingJobClient client = new();

            client.EnqueueJob<MarkedJob>(job => job.RunAsync(42));
            client.EnqueueJob<MarkedJob>(job => job.Run("sync"));

            Assert.Collection(client.Created,
                created =>
                {
                    Assert.Equal(typeof(MarkedJob), created.Job.Type);
                    Assert.Equal(nameof(MarkedJob.RunAsync), created.Job.Method.Name);
                    Assert.Equal(new object[] { 42 }, created.Job.Args);
                    Assert.IsType<EnqueuedState>(created.State);
                },
                created =>
                {
                    Assert.Equal(typeof(MarkedJob), created.Job.Type);
                    Assert.Equal(nameof(MarkedJob.Run), created.Job.Method.Name);
                    Assert.Equal(new object[] { "sync" }, created.Job.Args);
                });
        }

        [Fact]
        public void AddOrUpdateJob_schedules_the_marked_type_s_method_under_its_id_and_cron()
        {
            RecordingRecurringJobManager manager = new();
            TimeZoneInfo zone = TimeZoneInfo.CreateCustomTimeZone("Test+1", TimeSpan.FromHours(1), "Test+1", "Test+1");

            manager.AddOrUpdateJob<MarkedJob>("marked-nightly", job => job.RunAsync(7), "0 2 * * *",
                new RecurringJobOptions { TimeZone = zone });
            manager.AddOrUpdateJob<MarkedJob>("marked-hourly", job => job.Run("sync"), "0 * * * *");

            Assert.Collection(manager.Added,
                added =>
                {
                    Assert.Equal("marked-nightly", added.Id);
                    Assert.Equal(typeof(MarkedJob), added.Job.Type);
                    Assert.Equal(nameof(MarkedJob.RunAsync), added.Job.Method.Name);
                    Assert.Equal("0 2 * * *", added.Cron);
                    Assert.Same(zone, added.Options.TimeZone);
                },
                added =>
                {
                    Assert.Equal("marked-hourly", added.Id);
                    Assert.Equal(nameof(MarkedJob.Run), added.Job.Method.Name);
                    Assert.Equal("0 * * * *", added.Cron);
                    Assert.Equal(TimeZoneInfo.Utc, added.Options.TimeZone);
                });
        }

        [Fact]
        public void Every_typed_scheduling_method_accepts_only_a_marked_job()
        {
            MethodInfo[] methods = typeof(SpiderlyJobSchedulingExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static);

            Assert.Equal(
                new[] { "AddOrUpdateJob", "AddOrUpdateJob", "EnqueueJob", "EnqueueJob" },
                methods.Select(m => m.Name).Order());
            Assert.All(methods, m => Assert.Contains(typeof(ISpiderlyJob),
                Assert.Single(m.GetGenericArguments()).GetGenericParameterConstraints()));
        }

        public sealed class MarkedJob : ISpiderlyJob
        {
            public Task RunAsync(int id) => Task.CompletedTask;
            public void Run(string mode) { }
        }
    }
}
