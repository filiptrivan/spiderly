using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Shared
{
    public static class Validations
    {
        public static void ValidateDisplayNameAttributes(List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
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
                        throw SpiderlyDiagnostics.Error(
                            SpiderlyDiagnostics.DisplayNamePathInvalidProperty,
                            currentEntity.Location ?? entity.Location,
                            propertyName, currentEntity.Name);
                    }

                    if (i < parts.Length - 1)
                    {
                        SpiderlyClass targetEntity = ResolveDisplayNameNavigationTarget(currentEntity, property, allEntities);
                        currentEntity = targetEntity;
                    }
                }
            }
        }

        private static SpiderlyClass ResolveDisplayNameNavigationTarget(SpiderlyClass currentEntity, SpiderlyProperty property, List<SpiderlyClass> allEntities)
        {
            if (!property.Type.IsManyToOneType())
            {
                throw SpiderlyDiagnostics.Error(
                    SpiderlyDiagnostics.DisplayNameSegmentNotManyToOne,
                    property.Location ?? currentEntity.Location,
                    property.Name, currentEntity.Name);
            }

            SpiderlyClass targetEntity = allEntities.SingleOrDefault(x => x.Name == property.Type);

            if (targetEntity == null)
            {
                throw SpiderlyDiagnostics.Error(
                    SpiderlyDiagnostics.DisplayNameNavigationTargetNotFound,
                    property.Location ?? currentEntity.Location,
                    property.Type, property.Name, currentEntity.Name);
            }

            return targetEntity;
        }
    }
}
