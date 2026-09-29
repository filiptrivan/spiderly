using System.Reflection;
using System.Reflection.Emit;

namespace Spiderly.Infrastructure.Tests
{
    public class EntityTypeDiscoveryTests
    {
        /// <summary>
        /// A mocking library (Moq through Castle DynamicProxy, and EF's own lazy-loading proxies) emits
        /// proxy types into a runtime assembly, and a type it has defined but not yet finished cannot be
        /// loaded. A consumer's test suite builds models and lists entities on one thread while another
        /// thread emits a mock, so discovery that loads that assembly's types threw
        /// <see cref="ReflectionTypeLoadException"/> ("Could not load type
        /// 'Castle.Proxies.AuthorizationServiceProxy' from assembly 'DynamicProxyGenAssembly2'", PACMS,
        /// 2 of 111 hermetic runs, 2026-09-29). The unfinished type below holds that state for the whole
        /// call instead of for a race's window.
        /// </summary>
        [Fact]
        public void Discovery_does_not_throw_while_a_runtime_assembly_has_an_unfinished_type()
        {
            AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"MidEmission_{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
            TypeBuilder unfinished = assembly.DefineDynamicModule("Proxies")
                .DefineType("Proxies.ServiceProxy", TypeAttributes.Public | TypeAttributes.Class);

            try
            {
                List<Type> entityTypes = EntityTypeDiscovery.GetAllEntityTypes();

                // Still a sweep of the compiled assemblies, not a discovery that gave up and found nothing.
                Assert.Contains(typeof(RequirednessColumnNullabilityTests.NavTarget), entityTypes);
            }
            finally
            {
                // Finished, the type loads, so other tests in this process that discover entities are
                // not hit by this one's deliberately broken state.
                unfinished.CreateType();
            }
        }
    }
}
