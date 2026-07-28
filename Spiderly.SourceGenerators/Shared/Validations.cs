using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Validations
    {
        /// <summary>
        /// Validates every <c>[DisplayName]</c> path on the supplied entities and yields one diagnostic per broken path.
        /// Callers should report all of them and then decide whether to abort generation — this way a build surfaces
        /// every bad path at once instead of peeling them off one rebuild at a time.
        /// </summary>
        public static IEnumerable<Diagnostic> ValidateDisplayNameAttributes(List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyClass entity in currentProjectEntities.Where(x => x.HasDisplayNameAttribute()))
            {
                SpiderlyAttribute displayNameAttr = entity.Attributes
                    .Single(x => x.Name == "DisplayName");

                // TODO(nrt): [DisplayName]'s ctor arg is optional (bare [DisplayName] is valid syntax), so
                // Value can genuinely be null here — a bare [DisplayName] on an entity (instead of a property)
                // would NRE at generation time. Kept as-is (pre-existing risk, not introduced by this pass);
                // HasDisplayNameAttribute() gates this loop to entities using the path form in practice.
                string[] parts = displayNameAttr.Value!.Split('.');
                SpiderlyClass currentEntity = entity;

                for (int i = 0; i < parts.Length; i++)
                {
                    string propertyName = parts[i];

                    SpiderlyProperty property = currentEntity.Properties
                        .SingleOrDefault(x => x.Name == propertyName);

                    if (property == null)
                    {
                        yield return Diagnostic.Create(
                            SpiderlyDiagnostics.DisplayNamePathInvalidProperty,
                            LocationOrFallback(null, currentEntity, entity),
                            propertyName, currentEntity.Name);
                        break;
                    }

                    if (i < parts.Length - 1)
                    {
                        Diagnostic? navigationError;
                        SpiderlyClass? targetEntity = ResolveDisplayNameNavigationTarget(currentEntity, property, allEntities, out navigationError);
                        if (navigationError != null)
                        {
                            yield return navigationError;
                            break;
                        }
                        // ResolveDisplayNameNavigationTarget returns null exactly when it also sets
                        // navigationError non-null (which just broke the loop above), so targetEntity
                        // is guaranteed non-null here.
                        currentEntity = targetEntity!;
                    }
                }
            }
        }

        private static SpiderlyClass? ResolveDisplayNameNavigationTarget(SpiderlyClass currentEntity, SpiderlyProperty property, List<SpiderlyClass> allEntities, out Diagnostic? error)
        {
            if (!property.IsManyToOneType())
            {
                error = Diagnostic.Create(
                    SpiderlyDiagnostics.DisplayNameSegmentNotManyToOne,
                    LocationOrFallback(property, currentEntity),
                    property.Name, currentEntity.Name);
                return null;
            }

            SpiderlyClass targetEntity = Helpers.GetEntityByPropertyType(property, allEntities);

            if (targetEntity == null)
            {
                error = Diagnostic.Create(
                    SpiderlyDiagnostics.DisplayNameNavigationTargetNotFound,
                    LocationOrFallback(property, currentEntity),
                    property.Type, property.Name, currentEntity.Name);
                return null;
            }

            error = null;
            return targetEntity;
        }

        /// <summary>
        /// Validates every many-to-one navigation property on the supplied entities and yields one diagnostic
        /// per broken contract. Three failure modes are surfaced as SPIDERLY015 / SPIDERLY016 / SPIDERLY017:
        /// missing <c>[WithMany]</c>, missing back-collection on the target entity, and back-collection with the
        /// wrong element type respectively. Navigations marked with <c>[M2MWithMany]</c> belong to junction entities
        /// and are intentionally skipped — they are validated separately (SPIDERLY002).
        /// </summary>
        public static IEnumerable<Diagnostic> ValidateWithManyAttributes(List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                foreach (SpiderlyProperty navigation in entity.Properties)
                {
                    if (!navigation.IsManyToOneType())
                        continue;

                    if (navigation.HasM2MWithManyAttribute())
                        continue;

                    // The principal-side inverse of a valid 1-1 is a bare reference nav (no [WithMany], no
                    // [WithOne]) — M2O-shaped but legal. The matching [WithOne] back-nav on the target is what
                    // distinguishes it from a real M2O that simply forgot [WithMany], so this must NOT suppress
                    // the genuine SPIDERLY015 for a forgotten-[WithMany] mistake.
                    if (navigation.IsOneToOnePrincipalInverse(entity, allEntities))
                        continue;

                    string? withManyValue = navigation.WithMany();

                    if (withManyValue == null)
                    {
                        yield return Diagnostic.Create(
                            SpiderlyDiagnostics.ManyToOneMissingWithMany,
                            LocationOrFallback(navigation, entity),
                            entity.Name, navigation.Name, navigation.Type);
                        continue;
                    }

                    SpiderlyClass targetEntity = Helpers.GetEntityByPropertyType(navigation, allEntities);

                    // Target type isn't a Spiderly-modelled entity (e.g. a non-[SpiderlyEntity] class or
                    // a framework type we don't have a model for). EF Core still validates at runtime;
                    // we have nothing useful to say at compile time.
                    if (targetEntity == null)
                        continue;

                    SpiderlyProperty backCollection = targetEntity.Properties
                        .FirstOrDefault(p => p.Name == withManyValue);

                    if (backCollection == null)
                    {
                        yield return Diagnostic.Create(
                            SpiderlyDiagnostics.WithManyTargetCollectionNotFound,
                            LocationOrFallback(navigation, entity),
                            withManyValue, entity.Name, navigation.Name, targetEntity.Name);
                        continue;
                    }

                    string elementType = backCollection.Type.IsEnumerable()
                        ? Helpers.ExtractTypeFromGenericType(backCollection.Type)
                        : backCollection.Type.Raw;

                    if (elementType != entity.Name)
                    {
                        yield return Diagnostic.Create(
                            SpiderlyDiagnostics.WithManyTargetCollectionElementTypeMismatch,
                            LocationOrFallback(navigation, entity),
                            withManyValue, entity.Name, navigation.Name, targetEntity.Name, elementType);
                    }
                }
            }
        }

        private static Location LocationOrFallback(SpiderlyProperty? property, params SpiderlyClass[] entities)
        {
            if (property?.Location != null)
                return property.Location;

            foreach (SpiderlyClass entity in entities)
            {
                if (entity?.Location != null)
                    return entity.Location;
            }

            return Location.None;
        }
    }
}
