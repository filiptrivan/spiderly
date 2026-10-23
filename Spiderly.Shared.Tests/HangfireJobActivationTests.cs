using System.Linq.Expressions;
using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.Logging;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spiderly.Shared.Jobs;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Hangfire.NetCore's real server, over in-memory storage, builds each job with whatever <see cref="JobActivator"/>
    /// the container holds. Hangfire.NetCore registers its own with <c>TryAddSingleton</c>; once an app opts in with
    /// <c>spiderly.AddJobs(...)</c>, before or after <c>AddHangfire</c>, the server builds jobs only from the container.
    /// Without the opt-in it builds a job the container does not know, which is the gap the seam closes.
    /// </summary>
    [Collection(HangfireGlobals.Collection)]
    public class HangfireJobActivationTests
    {
        public enum OptIn { BeforeAddHangfire, AfterAddHangfire }

        [Theory]
        [InlineData(OptIn.BeforeAddHangfire)]
        [InlineData(OptIn.AfterAddHangfire)]
        public async Task Once_opted_in_the_server_refuses_a_job_the_container_does_not_know(OptIn optIn)
        {
            await using JobHost host = await JobHost.StartAsync(optIn);

            StateData state = await host.RunAsync<UnregisteredJob>(job => job.Run());

            Assert.Equal(FailedState.StateName, state.Name);
            Assert.Contains("is not registered in the container", state.Data["ExceptionMessage"]);
            Assert.Empty(host.Runs.Jobs);
        }

        [Theory]
        [InlineData(OptIn.BeforeAddHangfire)]
        [InlineData(OptIn.AfterAddHangfire)]
        public async Task Once_opted_in_the_server_builds_a_registered_job_from_the_container(OptIn optIn)
        {
            await using JobHost host = await JobHost.StartAsync(optIn);

            StateData state = await host.RunAsync<MarkedJob>(job => job.Run());

            Assert.Equal(SucceededState.StateName, state.Name);
            Assert.Equal(new[] { nameof(MarkedJob) }, host.Runs.Jobs);
        }

        [Fact]
        public async Task Without_opting_in_the_server_builds_a_job_the_container_does_not_know()
        {
            await using JobHost host = await JobHost.StartAsync(optIn: null);

            StateData state = await host.RunAsync<UnregisteredJob>(job => job.Run());

            Assert.Equal(SucceededState.StateName, state.Name);
            Assert.Equal(new[] { nameof(UnregisteredJob) }, host.Runs.Jobs);
        }

        /// <summary>A generic host running Hangfire.NetCore's server over its own in-memory storage.</summary>
        private sealed class JobHost : IAsyncDisposable
        {
            private readonly IHost _host;
            private readonly InMemoryStorage _storage;

            private JobHost(IHost host, InMemoryStorage storage)
            {
                _host = host;
                _storage = storage;
            }

            public RunLog Runs => _host.Services.GetRequiredService<RunLog>();

            public static async Task<JobHost> StartAsync(OptIn? optIn)
            {
                InMemoryStorage storage = new();
                IHost host = new HostBuilder()
                    .ConfigureServices(services =>
                    {
                        services.AddSingleton<RunLog>();
                        // Registered before AddHangfire, whose TryAdd then keeps it, so the client and the server
                        // use this host's storage rather than the process-wide JobStorage.Current.
                        services.AddSingleton<JobStorage>(storage);

                        if (optIn == OptIn.BeforeAddHangfire)
                            SpiderlyTestApp.AddSpiderly(services, spiderly => spiderly.AddJobs(typeof(JobHost).Assembly));

                        services.AddHangfire(_ => { });
                        services.AddHangfireServer(options => options.WorkerCount = 1);

                        if (optIn == OptIn.AfterAddHangfire)
                            SpiderlyTestApp.AddSpiderly(services, spiderly => spiderly.AddJobs(typeof(JobHost).Assembly));
                    })
                    .Build();

                await host.StartAsync();
                return new JobHost(host, storage);
            }

            /// <summary>Enqueues through Hangfire's own client, untyped, and waits for the job's final state.</summary>
            public async Task<StateData> RunAsync<TJob>(Expression<Action<TJob>> methodCall)
            {
                string jobId = _host.Services.GetRequiredService<IBackgroundJobClient>()
                    .Create(Job.FromExpression(methodCall), new EnqueuedState());

                using CancellationTokenSource timeout = new(TimeSpan.FromSeconds(30));
                while (true)
                {
                    using (IStorageConnection connection = _storage.GetConnection())
                    {
                        StateData? state = connection.GetStateData(jobId);
                        if (state != null && (state.Name == SucceededState.StateName || state.Name == FailedState.StateName))
                            return state;
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(20), timeout.Token);
                }
            }

            public async ValueTask DisposeAsync()
            {
                await _host.StopAsync();
                // AddHangfire points Hangfire's process-wide log provider and activator at this host's container;
                // left there, the next Hangfire object anywhere in the process logs to a disposed LoggerFactory.
                LogProvider.SetCurrentLogProvider(null);
                JobActivator.Current = new JobActivator();
                _host.Dispose();
            }
        }

        private sealed class RunLog
        {
            private readonly List<string> _jobs = new();

            public IReadOnlyList<string> Jobs { get { lock (_jobs) return _jobs.ToList(); } }

            public void Ran(string job) { lock (_jobs) _jobs.Add(job); }
        }

        [AutomaticRetry(Attempts = 0)]
        private sealed class MarkedJob : ISpiderlyJob
        {
            private readonly RunLog _runs;
            public MarkedJob(RunLog runs) => _runs = runs;
            public void Run() => _runs.Ran(nameof(MarkedJob));
        }

        // Unmarked, so the scan leaves it out: the job an app enqueues through Hangfire's own untyped API.
        [AutomaticRetry(Attempts = 0)]
        private sealed class UnregisteredJob
        {
            private readonly RunLog _runs;
            public UnregisteredJob(RunLog runs) => _runs = runs;
            public void Run() => _runs.Ran(nameof(UnregisteredJob));
        }
    }

    /// <summary>
    /// Tests that start Hangfire.NetCore, which writes Hangfire's process-wide configuration, run alone so no other
    /// test builds a Hangfire object while a host owns it.
    /// </summary>
    [CollectionDefinition(Collection, DisableParallelization = true)]
    public sealed class HangfireGlobals
    {
        public const string Collection = "Hangfire's process-wide configuration";
    }
}
