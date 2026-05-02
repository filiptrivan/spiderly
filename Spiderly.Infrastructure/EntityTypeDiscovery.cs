using Spiderly.Shared.Attributes.Entity;

namespace Spiderly.Infrastructure
{
    /// <summary>
    /// Discovers entity types at runtime via reflection — the single source of truth
    /// for what constitutes a Spiderly entity. Used by ApplicationDbContext for model
    /// registration and by consuming projects for runtime tasks like permission seeding.
    /// </summary>
    public static class EntityTypeDiscovery
    {
        /// <summary>
        /// Returns all types from loaded assemblies marked with [SpiderlyEntity].
        /// </summary>
        public static List<Type> GetAllEntityTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsDefined(typeof(SpiderlyEntityAttribute), inherit: false))
                .ToList();
        }
    }
}
