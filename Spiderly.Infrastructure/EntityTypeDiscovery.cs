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
        /// Returns all entity types from loaded assemblies whose namespace ends with ".Entities"
        /// and that inherit from BusinessObject/ReadonlyObject or are marked with [M2M].
        /// </summary>
        public static List<Type> GetAllEntityTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    t.Namespace != null &&
                    t.Namespace.EndsWith(".Entities") &&
                    (t.IsBusinessOrReadonlyEntity() || t.IsDefined(typeof(M2MAttribute), true)))
                .ToList();
        }
    }
}
