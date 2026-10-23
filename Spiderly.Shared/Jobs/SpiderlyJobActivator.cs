using Hangfire;
using Microsoft.Extensions.DependencyInjection;

namespace Spiderly.Shared.Jobs
{
    /// <summary>
    /// Hangfire <see cref="JobActivator"/> that builds a job only from the container, in one container scope per job.
    /// Hangfire.NetCore's own activator builds a job type the container does not know with
    /// <c>ActivatorUtilities.GetServiceOrCreateInstance</c>, outside the graph the host validated at boot; this one
    /// refuses it. Registered by <c>spiderly.AddJobs(...)</c>, which also registers every <see cref="ISpiderlyJob"/>.
    /// </summary>
    public sealed class SpiderlyJobActivator : JobActivator
    {
        private readonly IServiceScopeFactory _scopeFactory;

        /// <summary>Creates the activator over the application's root scope factory.</summary>
        public SpiderlyJobActivator(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        }

        /// <summary>Opens the container scope one job is built and run in.</summary>
        public override JobActivatorScope BeginScope(JobActivatorContext context)
            => new ContainerScope(_scopeFactory.CreateAsyncScope());

        /// <summary>Opens the container scope one job is built and run in (Hangfire's pre-1.8 entry point).</summary>
        [Obsolete("Please implement/use the BeginScope(JobActivatorContext) method instead. Will be removed in 2.0.0.")]
        public override JobActivatorScope BeginScope()
            => new ContainerScope(_scopeFactory.CreateAsyncScope());

        /// <summary>Always throws: a job is built only inside a job scope, never outside the container.</summary>
        public override object ActivateJob(Type jobType)
            => throw new InvalidOperationException(
                $"{nameof(SpiderlyJobActivator)} builds a job only inside a job scope (BeginScope), from the container.");

        private sealed class ContainerScope : JobActivatorScope
        {
            private readonly AsyncServiceScope _scope;

            public ContainerScope(AsyncServiceScope scope) => _scope = scope;

            public override object Resolve(Type type)
                => _scope.ServiceProvider.GetService(type)
                    ?? throw new InvalidOperationException(
                        $"The Hangfire job '{type.FullName}' is not registered in the container, so it is not built. "
                        + $"Mark it {nameof(ISpiderlyJob)} and pass its assembly to spiderly.AddJobs(...), "
                        + "or register it yourself (services.AddScoped<TJob>()).");

            // Asynchronously, as Hangfire.NetCore's own scope does: disposing a scope synchronously throws when it
            // holds a service that is only IAsyncDisposable. Hangfire disposes on a worker thread, so blocking here
            // cannot deadlock.
            public override void DisposeScope() => _scope.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
