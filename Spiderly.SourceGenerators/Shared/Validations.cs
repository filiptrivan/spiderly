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

                string[] parts = displayNameAttr.Value.Split('.');
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
                            currentEntity.Location ?? entity.Location ?? Location.None,
                            propertyName, currentEntity.Name);
                        break;
                    }

                    if (i < parts.Length - 1)
                    {
                        Diagnostic navigationError;
                        SpiderlyClass targetEntity = ResolveDisplayNameNavigationTarget(currentEntity, property, allEntities, out navigationError);
                        if (navigationError != null)
                        {
                            yield return navigationError;
                            break;
                        }
                        currentEntity = targetEntity;
                    }
                }
            }
        }

        private static SpiderlyClass ResolveDisplayNameNavigationTarget(SpiderlyClass currentEntity, SpiderlyProperty property, List<SpiderlyClass> allEntities, out Diagnostic error)
        {
            if (!property.Type.IsManyToOneType())
            {
                error = Diagnostic.Create(
                    SpiderlyDiagnostics.DisplayNameSegmentNotManyToOne,
                    property.Location ?? currentEntity.Location ?? Location.None,
                    property.Name, currentEntity.Name);
                return null;
            }

            SpiderlyClass targetEntity = allEntities.SingleOrDefault(x => x.Name == property.Type);

            if (targetEntity == null)
            {
                error = Diagnostic.Create(
                    SpiderlyDiagnostics.DisplayNameNavigationTargetNotFound,
                    property.Location ?? currentEntity.Location ?? Location.None,
                    property.Type, property.Name, currentEntity.Name);
                return null;
            }

            error = null;
            return targetEntity;
        }
    }
}
