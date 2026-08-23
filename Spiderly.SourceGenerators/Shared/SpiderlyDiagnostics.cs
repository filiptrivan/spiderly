using Microsoft.CodeAnalysis;
using System;

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
        public static SpiderlyGenerationException Create(DiagnosticDescriptor descriptor, Location? location, params object?[] args)
            => new SpiderlyGenerationException(Diagnostic.Create(descriptor, location ?? Location.None, args));

        /// <summary>
        /// Same as <see cref="IncrementalGeneratorInitializationContext.RegisterImplementationSourceOutput"/>
        /// but reports <see cref="SpiderlyGenerationException.Diagnostic"/> instead of letting the generator fault with CS8785.
        /// </summary>
        public static void RegisterSafeImplementationSourceOutput<TSource>(
            this IncrementalGeneratorInitializationContext context,
            IncrementalValueProvider<TSource> source,
            Action<SourceProductionContext, TSource> body)
        {
            context.RegisterImplementationSourceOutput(source, (spc, s) =>
            {
                try
                {
                    body(spc, s);
                }
                catch (SpiderlyGenerationException ex)
                {
                    spc.ReportDiagnostic(ex.Diagnostic);
                }
                catch (OperationCanceledException)
                {
                    // Roslyn cancels generators routinely (every keystroke in the IDE). Swallowing that
                    // would report a fake build error for ordinary editing.
                    throw;
                }
                catch (Exception ex)
                {
                    // Anything unexpected would otherwise become Roslyn's CS8785: a warning naming only
                    // the generator, with the generator silently contributing nothing. A build missing
                    // code it was promised must fail, and must say what died and why.
                    spc.ReportDiagnostic(Diagnostic.Create(
                        GeneratorFaulted,
                        Location.None,
                        GeneratorNameOf(body), ex.GetType().Name, ex.Message));
                }
            });
        }

        /// <summary>
        /// The generator type a handler belongs to. The delegate is declared inside the generator, so its
        /// declaring type is that generator (or its compiler-generated closure, whose parent it is).
        /// </summary>
        private static string GeneratorNameOf(Delegate body)
        {
            Type? type = body.Method.DeclaringType;

            while (type != null && type.Name.StartsWith("<", StringComparison.Ordinal))
                type = type.DeclaringType;

            return type?.Name ?? "<unknown generator>";
        }

        public static readonly DiagnosticDescriptor DisplayNameOnEntityRequiresPath = new(
            id: "SPIDERLY025",
            title: "[DisplayName] on an entity requires a property path",
            messageFormat: "[DisplayName] on entity '{0}' has no argument. On an entity it must name the property path used as the display value (e.g. [DisplayName(nameof(Name))] or [DisplayName(\"Category.Name\")]); the bare form is only valid on a property.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UITableColumnFieldNotFound = new(
            id: "SPIDERLY026",
            title: "[UITableColumn] field does not exist",
            messageFormat: "[UITableColumn(\"{0}\")] on '{2}.{1}' names a property that exists on neither the '{3}' entity nor its DTO. Check the spelling, or update the attribute if the property was renamed.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GenericTypeArgumentUnresolved = new(
            id: "SPIDERLY027",
            title: "Generic type argument could not be resolved for a base-class property",
            messageFormat: "Property '{0}' on base class '{1}' is typed as the generic parameter T, but its type argument could not be resolved. A generic base entity declared inside your own project is not supported — declare the property with a concrete type, or inherit BusinessObject<T> directly.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// <c>[Required]</c> is the source of truth for a column's nullability, so an entity property whose
        /// nullable annotation contradicts it has a C# type that lies. The two property kinds lie about
        /// different things, and both are worth failing a build over.
        /// <para>
        /// NAVIGATIONS — the attribute decides outright. <c>ConfigureManyToOneRelationships</c> /
        /// <c>ConfigureOneToOneRelationships</c> call <c>.IsRequired([Required] != null)</c> AFTER
        /// <c>.HasForeignKey()</c>, and that position is what makes it authoritative (measured in
        /// <c>Spiderly.Infrastructure.Tests.RequirednessColumnNullabilityTests</c>). So the annotation cannot
        /// move the column. What it does instead: a navigation annotated non-nullable over a NULLABLE foreign
        /// key makes EF materialize <c>null</c> into a non-nullable property, handing the consumer a
        /// NullReferenceException exactly where the type promised there could not be one.
        /// </para>
        /// <para>
        /// SCALARS — Spiderly configures no requiredness at all, so EF's conventions decide and they split by
        /// direction. <c>[Required]</c> on a nullable-annotated property still yields NOT NULL
        /// (<c>RequiredPropertyAttributeConvention</c> — a DataAnnotation source, which outranks the NRT
        /// convention). A non-nullable annotation with NO <c>[Required]</c> has nothing opposing
        /// <c>NonNullableReferencePropertyConvention</c>, so it silently makes the column NOT NULL: the next
        /// migration alters the schema, and a save that legitimately omits the value writes a default instead
        /// of NULL. Rejecting that disagreement here is what keeps <c>[Required]</c> authoritative for
        /// scalars too, and is why there is deliberately no runtime scalar-requiredness pass.
        /// </para>
        /// <para>
        /// Skipped entirely for a nullable-oblivious consumer, where a bare <c>Category</c> is the ABSENCE of
        /// an annotation rather than a claim of non-nullability.
        /// </para>
        /// </summary>
        public static readonly DiagnosticDescriptor NullabilityRequirednessMismatch = new(
            id: "SPIDERLY028",
            title: "Nullable annotation disagrees with [Required]",
            // Hint ({4}) sits before the rationale so the actionable half is adjacent to the problem, and so
            // the format ends on a period — RS1032 requires a multi-sentence message to be terminated.
            messageFormat: "'{0}.{1}' is {2} but is annotated '{3}'. {4} The annotation and [Required] are two statements about one column and must agree: a disagreement either silently alters the column's nullability or leaves the C# type lying about it.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor GeneratorFaulted = new(
            id: "SPIDERLY024",
            title: "A Spiderly source generator faulted",
            messageFormat: "Spiderly generator '{0}' faulted with {1}: {2}. It contributed no output, so code it generates is missing from this compilation. This is a bug in Spiderly — please report it with the entity shape that triggered it.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnresolvableControllerType = new(
            id: "SPIDERLY001",
            title: "Controller type is not a discovered entity or DTO",
            messageFormat: "Controller {0} type '{1}' on '{2}.{3}' is not discovered as an entity or DTO. The generated Angular client will reference an undefined TypeScript type. Mark the class with [SpiderlyDTO] (for DTOs) or [SpiderlyEntity] (for entities).",
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

        public static readonly DiagnosticDescriptor BlobPropertyMissingAcceptedFileTypes = new(
            id: "SPIDERLY014",
            title: "Blob property missing [AcceptedFileTypes] attribute",
            messageFormat: "Blob property '{0}.{1}' must declare [AcceptedFileTypes(\"mime/type\", ...)] with at least one MIME-typed value. Every blob property requires an explicit upload whitelist — there is no default.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ManyToOneMissingWithMany = new(
            id: "SPIDERLY015",
            title: "Many-to-one navigation missing [WithMany] attribute",
            messageFormat: "ManyToOne navigation '{0}.{1}' is missing [WithMany]. Add [WithMany(nameof({2}.<BackCollectionName>))] to this property and declare 'public virtual List<{0}> <BackCollectionName> {{ get; }} = new();' on '{2}'.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor WithManyTargetCollectionNotFound = new(
            id: "SPIDERLY016",
            title: "[WithMany] target collection does not exist on the related entity",
            messageFormat: "[WithMany(\"{0}\")] on '{1}.{2}' requires '{3}' to declare a 'List<{1}>' property named '{0}'. Add it to '{3}'.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor WithManyTargetCollectionElementTypeMismatch = new(
            id: "SPIDERLY017",
            title: "[WithMany] target collection has the wrong element type",
            messageFormat: "[WithMany(\"{0}\")] on '{1}.{2}' expects '{3}.{0}' to contain '{1}', but it contains '{4}'. Either fix the collection element type or change the [WithMany] target.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor UnsupportedPrimaryKeyType = new(
            id: "SPIDERLY018",
            title: "Entity primary key type must be int, long, or byte",
            messageFormat: "Entity '{0}' inherits '{1}<{2}>'. Spiderly primary keys must be int, long, or byte. Change '{2}' to one of those, or — if you need a public, non-enumerable identifier — keep a numeric Id and add a separate 'Guid PublicId' property.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OneToOneOnBothSides = new(
            id: "SPIDERLY019",
            title: "[WithOne] declared on both sides of a one-to-one",
            messageFormat: "Both '{0}.{1}' and '{2}.{3}' carry [WithOne]. Exactly one side (the dependent / FK holder) may carry it; remove [WithOne] from the principal and declare a plain single-valued navigation there.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OneToOneInverseNavNotFound = new(
            id: "SPIDERLY020",
            title: "[WithOne] inverse navigation does not exist on the principal entity",
            messageFormat: "[WithOne(\"{0}\")] on '{1}.{2}' requires '{3}' to declare a single-valued 'public virtual {1} {0}' navigation. Add it to '{3}', or use the parameterless [WithOne] for a unidirectional 1-1.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OneToOneRequiredOnPrincipal = new(
            id: "SPIDERLY021",
            title: "[Required] on the principal navigation of a one-to-one is unenforceable",
            messageFormat: "[Required] on principal navigation '{0}.{1}' is unenforceable: a unique FK index guarantees at most one dependent, never at least one. Configure requiredness on the dependent ([WithOne]) side instead, or enforce it in an OnAfterInsert hook.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor OneToOneSelfReferential = new(
            id: "SPIDERLY022",
            title: "Self-referential one-to-one is not supported",
            messageFormat: "[WithOne] on '{0}.{1}' targets the declaring entity '{0}'. Self-referential 1-1 is not supported in this version.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor ComplexManyToManyListWithoutAdditionalFields = new(
            id: "SPIDERLY023",
            title: "[ComplexManyToManyList] junction has no additional data columns",
            messageFormat: "Junction '{0}' referenced by [ComplexManyToManyList] on '{1}' has no data columns beside its two FKs. The generated form distinguishes linked rows from placeholders by their data columns, so a data-less junction would link every row on save. Use a plain collection (simple many-to-many) instead, or add a data column to '{0}'.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        /// <summary>
        /// Unlike the collection controls in <c>SpiderlyClassFactory</c> — where the id lookup was simply
        /// asked for too early and the fix was to move it into the branches that emit one — this generator
        /// genuinely requires the child's id: the emitted filter case is <c>values.Contains(x.Id)</c>. So the
        /// shape is unsupported rather than mis-sequenced, and the consumer needs a located diagnostic
        /// instead of SPIDERLY024's "This is a bug in Spiderly — please report it".
        /// </summary>
        /// <summary>
        /// A blob key prefix is the listing scope for save-time cleanup and staging promotion, so
        /// two properties sharing an effective prefix (or nesting one under another) would list and
        /// delete each other's objects — and prefixes land verbatim in public URLs, so a
        /// non-key-safe custom prefix would percent-encode. Raised by
        /// <see cref="BlobKeyPrefixValidator"/> before entity services are generated.
        /// </summary>
        public static readonly DiagnosticDescriptor InvalidBlobKeyPrefix = new(
            id: "SPIDERLY030",
            title: "Invalid or colliding blob KeyPrefix",
            messageFormat: "Blob key prefix '{0}' on '{1}.{2}': {3}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static readonly DiagnosticDescriptor CommaSeparatedDisplayNameOverKeylessJunction = new(
            id: "SPIDERLY029",
            title: "[GenerateCommaSeparatedDisplayName] targets a junction with no primary key",
            messageFormat: "[GenerateCommaSeparatedDisplayName] on '{0}.{1}' targets '{2}', a many-to-many junction with no primary key. The generated table filter matches the collection by child Id, which '{2}' does not have. Give '{2}' a BusinessObject<T> base so it has one, or drop the attribute.",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);
    }
}
