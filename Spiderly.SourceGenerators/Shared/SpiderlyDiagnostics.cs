using Microsoft.CodeAnalysis;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Registry of diagnostic descriptors emitted by Spiderly source generators.
    /// Add new IDs sequentially (SPIDERLY001, SPIDERLY002, ...). IDs are stable and must not be reused.
    /// </summary>
    public static class SpiderlyDiagnostics
    {
        public static readonly DiagnosticDescriptor UnresolvableControllerType = new(
            id: "SPIDERLY001",
            title: "Controller type is not a discovered entity or DTO",
            messageFormat: "Controller {0} type '{1}' on '{2}.{3}' is not discovered as an entity or DTO. The generated Angular client will reference an undefined TypeScript type. Place the class in a namespace ending with '.DTO', or make it inherit from a Spiderly entity base class (BusinessObject<T> / ReadonlyObject<T>).",
            category: "Spiderly.CodeGen",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
