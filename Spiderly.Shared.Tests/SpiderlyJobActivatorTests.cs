using Hangfire;
using Hangfire.Common;
using Hangfire.InMemory;
using Hangfire.Storage;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Jobs;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// <see cref="SpiderlyJobActivator"/> builds a job only from the container: a registered job comes back from the
    /// job's scope, an unregistered one is refused with the fix in the message, where Hangfire.NetCore's default
    /// activator would build it with <c>ActivatorUtilities</c>.
    /// </summary>
    public class SpiderlyJobActivatorTests
    {
        [Fact]
        public void Builds_a_registered_job_from_the_container()
        {
            using ServiceProvider provider = new ServiceCollection().AddScoped<RegisteredJob>().BuildServiceProvider();
            using JobActivatorScope scope = BeginScope(provider);

            Assert.IsType<RegisteredJob>(scope.Resolve(typeof(RegisteredJob)));
        }

        [Fact]
        public void Refuses_a_job_the_container_does_not_know()
        {
            using ServiceProvider provider = new ServiceCollection().AddScoped<RegisteredJob>().BuildServiceProvider();
            using JobActivatorScope scope = BeginScope(provider);

            InvalidOperationException refusal = Assert.Throws<InvalidOperationException>(
                () => scope.Resolve(typeof(UnregisteredJob)));

            Assert.Contains(typeof(UnregisteredJob).FullName!, refusal.Message);
            Assert.Contains(nameof(ISpiderlyJob), refusal.Message);
        }

        [Fact]
        public void A_scoped_job_lives_and_dies_with_its_job_scope()
        {
            using ServiceProvider provider = new ServiceCollection().AddScoped<RegisteredJob>().BuildServiceProvider();
            RegisteredJob first;
            using (JobActivatorScope scope = BeginScope(provider))
            {
                first = (RegisteredJob)scope.Resolve(typeof(RegisteredJob));
                Assert.Same(first, scope.Resolve(typeof(RegisteredJob)));
            }

            Assert.True(first.Disposed);
            using JobActivatorScope next = BeginScope(provider);
            Assert.NotSame(first, next.Resolve(typeof(RegisteredJob)));
        }

        private static JobActivatorScope BeginScope(ServiceProvider provider)
        {
            SpiderlyJobActivator activator = new(provider.GetRequiredService<IServiceScopeFactory>());
            InMemoryStorage storage = new();
            IStorageConnection connection = storage.GetConnection();
            BackgroundJob job = new("1", Job.FromExpression<RegisteredJob>(j => j.Run()), new DateTime(2026, 10, 24, 23, 30, 0, DateTimeKind.Utc));

            return activator.BeginScope(new JobActivatorContext(connection, job, new JobCancellationToken(false)));
        }

        private sealed class RegisteredJob : IDisposable
        {
            public bool Disposed { get; private set; }
            public void Run() { }
            public void Dispose() => Disposed = true;
        }

        private sealed class UnregisteredJob
        {
            public void Run() { }
        }
    }
}
