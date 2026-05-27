using System.Collections.Generic;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>Builds a <see cref="ShellComponentModel"/> from a <see cref="SpiderlyClass"/>.</summary>
    internal static class NgShellModelBuilder
    {
        internal static ShellComponentModel Build(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            string kebab = entity.Name.FromPascalToKebabCase();

            List<AdditionalSavePermissionCode> additionalSaveCodes = new();
            foreach (SpiderlyAttribute attribute in entity.Attributes)
            {
                if (attribute.Name == "UIAdditionalPermissionCodeForInsert")
                    additionalSaveCodes.Add(new AdditionalSavePermissionCode { PermissionCode = attribute.Value, ForInsert = true });
                else if (attribute.Name == "UIAdditionalPermissionCodeForUpdate")
                    additionalSaveCodes.Add(new AdditionalSavePermissionCode { PermissionCode = attribute.Value, ForInsert = false });
            }

            List<string> seedForkJoinParams = new();
            List<string> newEntitySeedInits = new();
            List<string> orderedChildSeedAssignments = new();

            // The entity's OWN top-level [ComplexManyToManyList]: fetch the backend default set and seed it on create.
            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                seedForkJoinParams.Add($"default{property.Name}For{entity.Name}: this.apiService.getDefault{property.Name}For{entity.Name}()");
                newEntitySeedInits.Add($"{property.Name.FirstCharToLower()}: data.default{property.Name}For{entity.Name}");
            }

            // DIRECT [UIOrderedOneToMany] children whose child entity has [ComplexManyToManyList]: fetch each default
            // and seed the child form array's formGroupInitialValues so a newly-added child row pre-populates them.
            // NOTE: we intentionally do NOT recurse deeper than one level. The legacy generator recursed and emitted a
            // {childCamel}FormGroup access expression that does not exist in the route-load scope (broken code that no
            // entity exercised). One level matches every real usage; do not "restore" the deeper recursion.
            foreach (SpiderlyProperty orderedProp in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass childEntity = Helpers.GetEntityByPropertyType(orderedProp, allEntities);
                if (childEntity == null)
                    continue;

                string formArray = $"this.parentFormGroup.controls.ordered{orderedProp.Name}SaveBodyDTO";

                foreach (SpiderlyProperty m2mProp in childEntity.GetComplexManyToManyListProperties())
                {
                    seedForkJoinParams.Add($"default{m2mProp.Name}For{childEntity.Name}: this.apiService.getDefault{m2mProp.Name}For{childEntity.Name}()");
                    orderedChildSeedAssignments.Add($"{formArray}.formGroupInitialValues = {{ ...{formArray}.formGroupInitialValues, {m2mProp.Name.FirstCharToLower()}: data.default{m2mProp.Name}For{childEntity.Name} }};");
                }
            }

            return new ShellComponentModel
            {
                EntityName = entity.Name,
                Selector = $"{kebab}-base-details",
                ComponentClassName = $"{entity.Name}BaseDetailsComponent",
                FieldsComponentClassName = $"{entity.Name}FieldsComponent",
                FieldsSelector = $"{kebab}-fields",
                SaveBodyTypeName = $"{entity.Name}SaveBody",
                MainUIFormTypeName = $"{entity.Name}MainUIForm",
                ConfigClassName = $"{entity.Name}FieldsConfig",
                DefaultAuthorized = !Helpers.ShouldAuthorizeEntity(entity),
                AdditionalSavePermissionCodes = additionalSaveCodes,
                SeedForkJoinParams = seedForkJoinParams,
                NewEntitySeedInits = newEntitySeedInits,
                OrderedChildSeedAssignments = orderedChildSeedAssignments,
            };
        }
    }
}
