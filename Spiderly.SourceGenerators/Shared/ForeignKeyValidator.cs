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

                string fkName = navigation.ResolveExplicitForeignKeyName(entity);
                if (fkName == null)
                    continue;

                SpiderlyProperty fkProperty = entity.Properties.First(p => p.Name == fkName);
                ValidateNullabilityAlignment(navigation, fkProperty, entity);
                ValidateForeignKeyTypeMatchesPrimaryKey(navigation, fkProperty, entity, allEntities);
            }
        }

        private static void ValidateForeignKeyAttributeTargets(SpiderlyProperty navigation, SpiderlyClass entity)
        {
            string fkFromNavAttribute = navigation.GetForeignKeyAttributeValue();
            if (fkFromNavAttribute != null && entity.Properties.Any(p => p.Name == fkFromNavAttribute) == false)
            {
                throw SpiderlyDiagnostics.Create(
                    SpiderlyDiagnostics.ForeignKeyPropertyNotFound,
                    navigation.Location ?? entity.Location,
                    fkFromNavAttribute, entity.Name, navigation.Name);
            }

            foreach (SpiderlyProperty scalar in entity.Properties.Where(p => p.Type.IsBaseDataType()))
            {
                string fkFromScalar = scalar.GetForeignKeyAttributeValue();
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
