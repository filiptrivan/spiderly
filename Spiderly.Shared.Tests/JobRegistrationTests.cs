using System.Reflection;
using Hangfire;
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Jobs;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// <c>spiderly.AddJobs(assemblies)</c> registers every class in those assemblies that carries <see cref="ISpiderlyJob"/>,
    /// and nothing else. Read from the descriptors AddSpiderly adds with the opt-in beyond those it adds without it.
    /// </summary>
    public class JobRegistrationTests
    {
        private static readonly Assembly ThisAssembly = typeof(JobRegistrationTests).Assembly;

        [Fact]
        public void Registers_every_marked_class_in_the_given_assemblies_as_scoped()
        {
            IReadOnlyList<ServiceDescriptor> added = AddedByOptIn(ThisAssembly);

            foreach (Type job in new[] { typeof(MarkedJob), typeof(JobFromMarkedBase), typeof(GenericJob<>) })
            {
                ServiceDescriptor descriptor = Assert.Single(added, d => d.ServiceType == job);
                Assert.Equal(job, descriptor.ImplementationType);
                Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            }
        }

        [Fact]
        public void NegativeControl_leaves_out_every_class_that_is_not_a_buildable_marked_job()
        {
            IReadOnlyList<ServiceDescriptor> added = AddedByOptIn(ThisAssembly);

            // It inspected this file's types: the marked class beside the controls is in.
            Assert.Contains(added, d => d.ServiceType == typeof(MarkedJob));
            foreach (Type control in new[] { typeof(UnmarkedJob), typeof(MarkedJobBase), typeof(IMarkedJobContract) })
                Assert.DoesNotContain(added, d => d.ServiceType == control);

            // Across the whole assembly: nothing beside marked jobs, and the activator.
            Assert.All(added, d => Assert.True(
                d.ServiceType == typeof(JobActivator) || typeof(ISpiderlyJob).IsAssignableFrom(d.ServiceType),
                $"{d.ServiceType} is not a job"));
        }

        [Fact]
        public void Registers_only_from_the_assemblies_it_is_given()
        {
            IReadOnlyList<ServiceDescriptor> added = AddedByOptIn(typeof(object).Assembly);

            Assert.DoesNotContain(added, d => d.ServiceType == typeof(MarkedJob));
            Assert.Contains(added, d => d.ServiceType == typeof(JobActivator));
        }

        [Fact]
        public void A_second_call_adds_its_assemblies_to_the_first_call_s()
        {
            IReadOnlyList<ServiceDescriptor> added = AddedByOptIn(spiderly => spiderly
                .AddJobs(ThisAssembly)
                .AddJobs(typeof(object).Assembly));

            Assert.Contains(added, d => d.ServiceType == typeof(MarkedJob));
        }

        private static IReadOnlyList<ServiceDescriptor> AddedByOptIn(params Assembly[] assemblies)
            => AddedByOptIn(spiderly => spiderly.AddJobs(assemblies));

        private static IReadOnlyList<ServiceDescriptor> AddedByOptIn(Action<SpiderlyBuilder> optIn)
        {
            ServiceCollection without = new();
            SpiderlyTestApp.AddSpiderly(without, _ => { });
            ServiceCollection with = new();
            SpiderlyTestApp.AddSpiderly(with, optIn);

            HashSet<(Type, Type?, ServiceLifetime)> before = without.Select(Key).ToHashSet();
            return with.Where(d => !before.Contains(Key(d))).ToList();

            static (Type, Type?, ServiceLifetime) Key(ServiceDescriptor d) => (d.ServiceType, d.ImplementationType, d.Lifetime);
        }

        private sealed class MarkedJob : ISpiderlyJob
        {
            public void Run() { }
        }

        private abstract class MarkedJobBase : ISpiderlyJob
        {
            public void Run() { }
        }

        private sealed class JobFromMarkedBase : MarkedJobBase
        {
        }

        private sealed class GenericJob<T> : ISpiderlyJob
        {
            public void Run() { }
        }

        private interface IMarkedJobContract : ISpiderlyJob
        {
        }

        private sealed class UnmarkedJob
        {
            public void Run() { }
        }
    }
}
