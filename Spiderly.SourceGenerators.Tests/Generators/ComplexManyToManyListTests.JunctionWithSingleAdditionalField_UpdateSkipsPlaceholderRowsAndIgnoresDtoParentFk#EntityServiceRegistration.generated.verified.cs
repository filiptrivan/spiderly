//HintName: EntityServiceRegistration.generated.cs
using Microsoft.Extensions.DependencyInjection;
using Spiderly.Shared.Services;

namespace TestApp.Business.Services
{
    /// <summary>
    /// Registers all entity services in the DI container.
    /// Call <c>services.AddEntityServices()</c> in your startup configuration.
    /// </summary>
    public static class EntityServiceRegistration
    {
        public static IServiceCollection AddEntityServices(this IServiceCollection services)
        {
            services.AddTransient(typeof(Lazy<>), typeof(LazyServiceProvider<>));
            services.AddTransient<EntityServiceDependencies>();
            services.AddTransient<AuthorizationServiceGenerated>();

            services.AddTransient<ItemServiceGenerated>();
            services.AddTransient<ItemWarehouseServiceGenerated>();
            services.AddTransient<WarehouseServiceGenerated>();

            return services;
        }
    }
}