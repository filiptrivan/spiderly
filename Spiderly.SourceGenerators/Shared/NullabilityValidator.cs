using Spiderly.SourceGenerators.Models;
using Microsoft.CodeAnalysis;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// SPIDERLY028 — an entity property's nullable annotation must agree with <c>[Required]</c>.
    /// See <see cref="SpiderlyDiagnostics.NullabilityRequirednessMismatch"/> for what a disagreement costs.
    /// <para>
    /// Separate from <see cref="ForeignKeyValidator"/>, which validates explicit foreign-key declarations:
    /// this sweeps every property, plain scalars included.
    /// </para>
    /// </summary>
    public static class NullabilityValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, NullableContextOptions nullableContext)
        {
            // Nothing to disagree in a nullable-oblivious consumer: a bare 'Category' there is the ABSENCE
            // of an annotation, not a claim of non-nullability.
            if (nullableContext.AnnotationsEnabled() == false)
                return;

            foreach (SpiderlyProperty property in entity.Properties)
                ValidateAnnotationAgreesWithRequired(property, entity);
        }

        private static void ValidateAnnotationAgreesWithRequired(SpiderlyProperty property, SpiderlyClass entity)
        {
            // A value type carries its nullability in the CLR type, so 'int' and 'int?' can't disagree with
            // anything. Enums likewise. Collections are '= new()' and never null.
            if (property.IsEnum || property.Type.IsEnumerable())
                return;

            if (property.Type.IsBaseDataType() && property.Type.IsReferenceTypeScalar == false)
                return;

            // They agree when exactly one is true: required+non-nullable, or optional+nullable.
            bool isRequired = property.IsEffectivelyRequired();
            if (isRequired != property.Type.IsNullable)
                return;

            throw SpiderlyDiagnostics.Create(
                SpiderlyDiagnostics.NullabilityRequirednessMismatch,
                property.Location ?? entity.Location,
                entity.Name, property.Name,
                isRequired ? "[Required]" : "optional",
                property.Type,
                isRequired
                    ? "Drop the '?' (initialize with '= null!;'), or remove [Required] if the value really is optional."
                    : "Annotate it nullable ('?'), or add [Required] if the value really is mandatory.");
        }
    }
}
