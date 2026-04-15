using Microsoft.CodeAnalysis;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Registry of diagnostic descriptors emitted by Spiderly source generators.
    /// Add new IDs sequentially (SPIDERLY001, SPIDERLY002, ...). IDs are stable and must not be reused.
    /// </summary>
    public static class SpiderlyDiagnostics
    {
        private const string Category = "Spiderly.CodeGen";

        /// <summary>
        /// Builds a <see cref="SpiderlyGenerationException"/> carrying a located diagnostic.
        /// Pass <paramref name="location"/> as <c>null</c> to use <see cref="Location.None"/>.
        /// </summary>
        public static SpiderlyGenerationException Error(DiagnosticDescriptor descriptor, Location location, params object[] args)
            => new SpiderlyGenerationException(Diagnostic.Create(descriptor, location ?? Location.None, args));

        public static readonly DiagnosticDescriptor UnresolvableControllerType = new(
            id: "SPIDERLY001",
            title: "Controller type is not a discovered entity or DTO",
            messageFormat: "Controller {0} type '{1}' on '{2}.{3}' is not discovered as an entity or DTO. The generated Angular client will reference an undefined TypeScript type. Place the class in a namespace ending with '.DTO', or make it inherit from a Spiderly entity base class (BusinessObject<T> / ReadonlyObject<T>).",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ManyToManyRequiresExactlyTwoWithMany = new(
            id: "SPIDERLY002",
            title: "Many-to-many entity requires exactly two [M2MWithMany] properties",
            messageFormat: "Entity '{0}' is a many-to-many join but has {1} properties marked with [M2MWithMany] — exactly two are required, one on each side of the join",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForeignKeyPropertyNotFound = new(
            id: "SPIDERLY003",
            title: "[ForeignKey] references a property that does not exist",
            messageFormat: "[ForeignKey(nameof({0}))] on '{1}.{2}' references a property that does not exist on '{1}'",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForeignKeyTypeMismatch = new(
            id: "SPIDERLY004",
            title: "Foreign key type does not match target primary key",
            messageFormat: "Foreign key '{0}.{1}' is '{2}' but target '{3}.Id' is '{4}'. FK and PK types must match.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForeignKeyAmbiguous = new(
            id: "SPIDERLY005",
            title: "Foreign key is ambiguous — multiple convention matches",
            messageFormat: "Ambiguous FK pair for '{0}.{1}' — multiple scalar properties match convention '{2}'. Use [ForeignKey(nameof(...))] to disambiguate.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ForeignKeyNullabilityMismatch = new(
            id: "SPIDERLY006",
            title: "Foreign key nullability does not match navigation property",
            messageFormat: "Nullability mismatch on '{0}': navigation '{1}' is {2} but FK '{3}' is {4} ({5}) — {6}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DisplayNamePathInvalidProperty = new(
            id: "SPIDERLY007",
            title: "[DisplayName] path references a property that does not exist",
            messageFormat: "'{0}' is not a property on class '{1}' (referenced by [DisplayName])",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DisplayNameSegmentNotManyToOne = new(
            id: "SPIDERLY008",
            title: "[DisplayName] path segment is not a many-to-one navigation",
            messageFormat: "'{0}' on class '{1}' is not a many-to-one navigation property and cannot be used in a [DisplayName] path",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor DisplayNameNavigationTargetNotFound = new(
            id: "SPIDERLY009",
            title: "[DisplayName] navigation target entity not found",
            messageFormat: "Could not find entity '{0}' referenced by property '{1}' on class '{2}' (while resolving [DisplayName] path)",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor EntityMissingBusinessObjectBase = new(
            id: "SPIDERLY010",
            title: "Entity missing required BusinessObject<T> / ReadonlyObject<T> base",
            messageFormat: "Entity '{0}' (base: '{1}') cannot resolve its Id type. Every Spiderly entity must inherit — directly or transitively — from BusinessObject<T> or ReadonlyObject<T>.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ControllerPropertyTypeUnresolvable = new(
            id: "SPIDERLY011",
            title: "Controller property type is not resolvable for client generation",
            messageFormat: "Property '{0}.{1}' of type '{2}' cannot be resolved to an entity or DTO for Angular client generation",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OneToManyMissingM2MWithMany = new(
            id: "SPIDERLY012",
            title: "One-to-many back-reference missing [M2MWithMany]",
            messageFormat: "Entity '{0}' declares a one-to-many collection that requires a matching [M2MWithMany] attribute on the opposite side of the join but none was found",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor BackendFolderNotFound = new(
            id: "SPIDERLY013",
            title: "Backend folder not found under calling project",
            messageFormat: "Folder '{0}' not found in path '{1}'. File-emitting generators will be skipped.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true);
    }
}
