using Microsoft.Extensions.DependencyInjection;

namespace Spiderly.Shared.Services
{
    /// <summary>
    /// Enables <c>Lazy&lt;T&gt;</c> injection in the DI container.
    /// Defers service resolution until <c>.Value</c> is accessed, which breaks circular dependency chains
    /// between per-entity services that reference each other (e.g., parent-child ordered one-to-many relationships).
    /// <example>
    /// Register once in DI:
    /// <code>services.AddTransient(typeof(Lazy&lt;&gt;), typeof(LazyServiceProvider&lt;&gt;));</code>
    /// Then inject <c>Lazy&lt;ProductEntityServiceGenerated&gt;</c> in any service constructor.
    /// </example>
    /// </summary>
    public class LazyServiceProvider<T> : Lazy<T> where T : class
    {
        public LazyServiceProvider(IServiceProvider provider)
            : base(() => provider.GetRequiredService<T>()) { }
    }
}
