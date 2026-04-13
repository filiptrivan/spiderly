using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    /// <summary>
    /// Compile-time validation for explicit foreign key declarations on Spiderly entities.
    /// Throws descriptive exceptions (matching Spiderly's fail-loud convention) so build
    /// output surfaces the exact entity and property where the contract is broken.
    ///
    /// Error codes (grep-friendly):
    /// - SPID001 — Nullability mismatch between navigation and FK scalar
    /// - SPID002 — [ForeignKey(nameof(X))] references a property that doesn't exist
    /// - SPID003 — FK scalar type doesn't match the target entity's primary key type
    /// - SPID004 — Ambiguous convention match: multiple scalars could pair with the same nav
    /// </summary>
    public static class ForeignKeyValidator
    {
        public static void ValidateEntity(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyProperty navigation in entity.Properties.Where(p => p.Type.IsManyToOneType()))
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
                throw new Exception(
                    $"SPID002: [ForeignKey(nameof({fkFromNavAttribute}))] on {entity.Name}.{navigation.Name} " +
                    $"references a property that does not exist on {entity.Name}.");
            }

            foreach (SpiderlyProperty scalar in entity.Properties.Where(p => p.Type.IsBaseDataType()))
            {
                string fkFromScalar = scalar.GetForeignKeyAttributeValue();
                if (fkFromScalar != null && entity.Properties.Any(p => p.Name == fkFromScalar) == false)
                {
                    throw new Exception(
                        $"SPID002: [ForeignKey(nameof({fkFromScalar}))] on {entity.Name}.{scalar.Name} " +
                        $"references a navigation property that does not exist on {entity.Name}.");
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
                throw new Exception(
                    $"SPID004: Ambiguous FK pair for {entity.Name}.{navigation.Name} — multiple scalar " +
                    $"properties match convention '{conventionName}'. Use [ForeignKey(nameof(...))] to disambiguate.");
            }
        }

        private static void ValidateNullabilityAlignment(SpiderlyProperty navigation, SpiderlyProperty fkProperty, SpiderlyClass entity)
        {
            bool navIsRequired = navigation.HasRequiredAttribute();
            bool fkIsNullable = fkProperty.Type.TrimEnd().EndsWith("?");

            if (navIsRequired && fkIsNullable)
            {
                throw new Exception(
                    $"SPID001: Nullability mismatch on {entity.Name} — navigation '{navigation.Name}' has " +
                    $"[Required] but FK '{fkProperty.Name}' is nullable ({fkProperty.Type}). " +
                    $"Either drop [Required] or make the FK non-nullable.");
            }

            if (navIsRequired == false && fkIsNullable == false)
            {
                throw new Exception(
                    $"SPID001: Nullability mismatch on {entity.Name} — navigation '{navigation.Name}' is " +
                    $"optional but FK '{fkProperty.Name}' is non-nullable ({fkProperty.Type}). " +
                    $"Either add [Required] to the navigation or make the FK nullable.");
            }
        }

        private static void ValidateForeignKeyTypeMatchesPrimaryKey(
            SpiderlyProperty navigation,
            SpiderlyProperty fkProperty,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities)
        {
            SpiderlyClass targetEntity = allEntities.FirstOrDefault(c => c.Name == navigation.Type);
            if (targetEntity == null)
                return;

            string targetIdType;
            try
            {
                targetIdType = targetEntity.GetIdType(allEntities);
            }
            catch
            {
                return;
            }

            string fkType = fkProperty.Type.TrimEnd().TrimEnd('?');

            if (fkType != targetIdType)
            {
                throw new Exception(
                    $"SPID003: FK type mismatch on {entity.Name} — '{fkProperty.Name}' is {fkProperty.Type} " +
                    $"but target {targetEntity.Name}.Id is {targetIdType}. FK and PK types must match.");
            }
        }
    }
}
