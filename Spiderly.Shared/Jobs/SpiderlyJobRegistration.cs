using System.Reflection;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Spiderly.Shared.Jobs
{
    /// <summary>What <c>spiderly.AddJobs(...)</c> registers; called from <c>AddSpiderly</c>.</summary>
    internal static class SpiderlyJobRegistration
    {
        public static void AddJobs(IServiceCollection services, IEnumerable<Assembly> assemblies)
        {
            foreach (Type job in assemblies.SelectMany(a => a.GetTypes()).Where(IsJob))
            {
                // TryAdd, so an app that registered a job itself (another lifetime, a factory) keeps its registration.
                services.TryAddScoped(job);
            }

            // Hangfire.NetCore's AddHangfire registers its own activator with TryAddSingleton, and its server builds
            // jobs with whichever JobActivator the container holds. Removing any earlier registration makes ours that
            // one whether AddHangfire ran before AddSpiderly or runs after it (then its TryAdd finds ours).
            services.RemoveAll<JobActivator>();
            services.AddSingleton<JobActivator, SpiderlyJobActivator>();
        }

        private static bool IsJob(Type type)
            => type.IsClass && !type.IsAbstract && typeof(ISpiderlyJob).IsAssignableFrom(type);
    }
}
