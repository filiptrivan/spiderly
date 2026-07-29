using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Compile-time validation for explicit foreign key declarations on Spiderly entities.
    /// Throws <see cref="SpiderlyGenerationException"/> carrying a located SPIDERLY### diagnostic
    /// so build output surfaces the exact entity and property where the contract is broken
    /// (instead of a CS8785 "generator failed" stack trace).
    /// </summary>
    public static class ForeignKeyValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            // Type/nullability-check every FK-bearing reference nav (M2O + 1-1 dependent) — SPIDERLY004/006.
            // Disjoint from OneToOneValidator's SPIDERLY019-022, so no double-reporting on a valid 1-1.
            foreach (SpiderlyProperty navigation in entity.Properties.Where(p => p.IsForeignKeyReferenceNav()))
            {
                ValidateForeignKeyAttributeTargets(navigation, entity);
                ValidateConventionAmbiguity(navigation, entity);

                string? fkName = navigation.ResolveExplicitForeignKeyName(entity);
                if (fkName == null)
                    continue;

                SpiderlyProperty fkProperty = entity.Properties.First(p => p.Name == fkName);
                ValidateNullabilityAlignment(navigation, fkProperty, entity);
                ValidateForeignKeyTypeMatchesPrimaryKey(navigation, fkProperty, entity, allEntities);
            }
        }

        private static void ValidateForeignKeyAttributeTargets(SpiderlyProperty navigation, SpiderlyClass entity)
        {
            string? fkFromNavAttribute = navigation.GetForeignKeyAttributeValue();
            if (fkFromNavAttribute != null && entity.Properties.Any(p => p.Name == fkFromNavAttribute) == false)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyPropertyNotFound,
                    navigation.Location ?? entity.Location,
                    fkFromNavAttribute, entity.Name, navigation.Name);
            }

            foreach (SpiderlyProperty scalar in entity.Properties.Where(p => p.Type.IsBaseDataType()))
            {
                string? fkFromScalar = scalar.GetForeignKeyAttributeValue();
                if (fkFromScalar != null && entity.Properties.Any(p => p.Name == fkFromScalar) == false)
                {
                    throw SpiderlyDiagnostics.Create(
                        SpiderlyDiagnostics.ForeignKeyPropertyNotFound,
                        scalar.Location ?? entity.Location,
                        fkFromScalar, entity.Name, scalar.Name);
                }
            }
        }

        private static void ValidateConventionAmbiguity(SpiderlyProperty navigation, SpiderlyClass entity)
        {
            if (navigation.HasForeignKeyAttribute())
                return;

            bool anyScalarPointsBackByAttribute = entity.Properties.Any(p =>
                p.Type.IsBaseDataType() && p.GetForeignKeyAttributeValue() == navigation.Name);
            if (anyScalarPointsBackByAttribute)
                return;

            string conventionName = $"{navigation.Name}Id";
            int conventionCandidates = entity.Properties.Count(p => p.Name == conventionName && p.Type.IsBaseDataType());

            if (conventionCandidates > 1)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyAmbiguous,
                    navigation.Location ?? entity.Location,
                    entity.Name, navigation.Name, conventionName);
            }
        }

        /// <summary>
        /// SPIDERLY028 for every property of <paramref name="entity"/>.
        /// </summary>
        /// <param name="annotationsEnabled">
        /// Whether the CONSUMER's compilation is <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c>. A
        /// nullable-oblivious consumer writes <c>public virtual Category Category</c> with no <c>?</c>, and
        /// that is the ABSENCE of an annotation rather than a claim of non-nullability — nothing there can
        /// disagree, so the whole check is skipped.
        /// </param>
        public static void ValidateNullabilityMatchesRequiredness(SpiderlyClass entity, bool annotationsEnabled)
        {
            if (annotationsEnabled == false)
                return;

            foreach (SpiderlyProperty property in entity.Properties)
                ValidateNullabilityMatchesRequiredness(property, entity);
        }

        /// <summary>
        /// SPIDERLY028 — the property's nullable annotation must agree with <c>[Required]</c>, because under
        /// an annotated context the annotation is what EF turns into the column's nullability. Applies to
        /// reference-typed scalars and to reference navigations alike; a value type carries its nullability
        /// in the CLR type, so <c>int</c> and <c>int?</c> cannot disagree with anything.
        /// </summary>
        private static void ValidateNullabilityMatchesRequiredness(SpiderlyProperty property, SpiderlyClass entity)
        {
            // Collections are never null (initialized '= new()'), and enums/value types carry their own
            // nullability. Only reference-typed scalars and navigations are at stake.
            if (property.Type.IsEnumerable() || property.IsEnum)
                return;

            bool isReferenceType = property.Type.IsReferenceTypeScalar || property.Type.IsManyToOneType();
            if (isReferenceType == false)
                return;

            bool isRequired = property.IsEffectivelyRequired();
            bool isNullable = property.Type.IsNullable;

            if (isRequired && isNullable)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.NullabilityRequirednessMismatch,
                    property.Location ?? entity.Location,
                    entity.Name, property.Name, "[Required]", $"{property.Type.CoreName}?", "nullable",
                    "Drop the '?' (initialize with '= null!;'), or remove [Required] if the value really is optional.");
            }

            if (isRequired == false && isNullable == false)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.NullabilityRequirednessMismatch,
                    property.Location ?? entity.Location,
                    entity.Name, property.Name, "optional", property.Type.CoreName, "NOT NULL",
                    "Annotate it nullable ('?'), or add [Required] if the value really is mandatory — the latter changes the schema, so take the migration.");
            }
        }

        private static void ValidateNullabilityAlignment(SpiderlyProperty navigation, SpiderlyProperty fkProperty, SpiderlyClass entity)
        {
            bool navIsRequired = navigation.IsEffectivelyRequired();
            bool fkIsNullable = fkProperty.Type.Raw.TrimEnd().EndsWith("?");

            if (navIsRequired && fkIsNullable)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyNullabilityMismatch,
                    fkProperty.Location ?? navigation.Location ?? entity.Location,
                    entity.Name, navigation.Name, "[Required]", fkProperty.Name, "nullable", fkProperty.Type,
                    "either drop [Required] or make the FK non-nullable");
            }

            if (navIsRequired == false && fkIsNullable == false)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyNullabilityMismatch,
                    fkProperty.Location ?? navigation.Location ?? entity.Location,
                    entity.Name, navigation.Name, "optional", fkProperty.Name, "non-nullable", fkProperty.Type,
                    "either add [Required] to the navigation or make the FK nullable");
            }
        }

        private static void ValidateForeignKeyTypeMatchesPrimaryKey(
            SpiderlyProperty navigation,
            SpiderlyProperty fkProperty,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities)
        {
            SpiderlyClass targetEntity = allEntities.FirstOrDefault(c => c.Name == navigation.Type.Name);
            if (targetEntity == null)
                return;

            string targetIdType = targetEntity.GetIdType(allEntities);
            string fkType = fkProperty.Type.Raw.WithoutNullableSuffix();

            if (fkType != targetIdType)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyTypeMismatch,
                    fkProperty.Location ?? entity.Location,
                    entity.Name, fkProperty.Name, fkProperty.Type, targetEntity.Name, targetIdType);
            }
        }
    }
}
