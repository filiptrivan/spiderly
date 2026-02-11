using System;
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
                        throw new Exception($"'{propertyName}' is not a property on class '{currentEntity.Name}'.");

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
                throw new Exception($"'{property.Name}' on class '{currentEntity.Name}' is not a many-to-one navigation property and cannot be used in a DisplayName path.");

            SpiderlyClass targetEntity = allEntities.SingleOrDefault(x => x.Name == property.Type);

            if (targetEntity == null)
                throw new Exception($"Could not find entity '{property.Type}' referenced by property '{property.Name}' on class '{currentEntity.Name}'.");

            return targetEntity;
        }

    }
}