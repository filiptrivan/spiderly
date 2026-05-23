using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Net
{
    internal static class ServiceM2MGenerator
    {
        internal static string GetManyToManyData(SpiderlyClass entity, List<SpiderlyClass> allEntityClasses)
        {
            if (entity.Properties.Count == Settings.NumberOfPropertiesWithoutAdditionalManyToManyProperties)
                return null;

            List<SpiderlyProperty> properties = entity.Properties;

            List<SpiderlyProperty> m2mWithManyProperties = properties
                .Where(x => x.HasM2MWithManyAttribute())
                .ToList();

            if (m2mWithManyProperties.Count != 2)
                return "YouNeedToDefineTwoM2MWithManyProperties";

            SpiderlyProperty m2mWithManyProperty_1 = m2mWithManyProperties[0];
            SpiderlyAttribute m2mWithManyAttribute_1 = m2mWithManyProperty_1.Attributes.Single(x => x.Name == "M2MWithMany");

            SpiderlyProperty m2mWithManyProperty_2 = m2mWithManyProperties[1];
            SpiderlyAttribute m2mWithManyAttribute_2 = m2mWithManyProperty_2.Attributes.Single(x => x.Name == "M2MWithMany");

            if (m2mWithManyAttribute_1.Value != m2mWithManyAttribute_2.Value)
                return null; // It's simple M2M

            SpiderlyClass m2mEntity_1 = allEntityClasses.Single(x => x.Name == m2mWithManyProperty_1.Type.Raw);
            string m2mEntityIdType_1 = m2mEntity_1.GetIdType(allEntityClasses);

            SpiderlyClass m2mEntity_2 = allEntityClasses.Single(x => x.Name == m2mWithManyProperty_2.Type.Raw);
            string m2mEntityIdType_2 = m2mEntity_2.GetIdType(allEntityClasses);

            return $$"""
{{GetComplexManyToManyAdministrationMethod(m2mWithManyProperty_1, m2mWithManyProperty_2, m2mEntityIdType_1, m2mEntityIdType_2, entity, allEntityClasses)}}

{{GetComplexManyToManyAdministrationMethod(m2mWithManyProperty_2, m2mWithManyProperty_1, m2mEntityIdType_2, m2mEntityIdType_1, entity, allEntityClasses)}}
""";
        }

        internal static string GetComplexManyToManyAdministrationMethod(
            SpiderlyProperty m2mWithManyProperty_1,
            SpiderlyProperty m2mWithManyProperty_2,
            string m2mEntityIdType_1,
            string m2mEntityIdType_2,
            SpiderlyClass entity,
            List<SpiderlyClass> entities
        )
        {
            return $$"""
        /// <summary>
        /// Updates a complex many-to-many relationship with additional fields in the association entity.
        /// Use this for M2M relationships that have extra properties beyond the foreign keys (e.g., OrderProduct with Quantity, Price).
        /// Validates each DTO, adds new associations, updates existing ones, and removes unselected associations.
        /// </summary>
        /// <param name="{{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id">The ID of the {{m2mWithManyProperty_2.Type}} entity</param>
        /// <param name="selected{{entity.Name}}DTOList">List of {{entity.Name}}DTOs representing the associations with additional fields</param>
        public async virtual Task Update{{m2mWithManyProperty_1.Type}}ListFor{{m2mWithManyProperty_2.Type}}({{m2mEntityIdType_2}} {{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id, List<{{entity.Name}}DTO> selected{{entity.Name}}DTOList)
        {
            if (selected{{entity.Name}}DTOList == null)
                return;

            List<{{entity.Name}}DTO> selectedDTOListHelper = selected{{entity.Name}}DTOList.ToList();

            await _deps.Context.WithTransactionAsync(async () =>
            {
                // Not doing authorization here, because we can not figure out here if we are updating while inserting object (eg. User), or updating object, we will always get the id which is not 0 here.

                var dbSet = _deps.Context.DbSet<{{entity.Name}}>();
                var {{entity.Name.FirstCharToLower()}}List = await dbSet.Where(x => {{m2mWithManyProperty_2.GetForeignKeyAccessExpression(entity, entities)}} == {{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id).ToListAsync();

                foreach ({{entity.Name}}DTO selected{{entity.Name}}DTO in selectedDTOListHelper)
                {
                    var validationRules = new {{entity.Name}}DTOValidationRules();
                    DefaultValidatorExtensions.ValidateAndThrow(validationRules, selected{{entity.Name}}DTO);

                    var {{entity.Name.FirstCharToLower()}} = {{entity.Name.FirstCharToLower()}}List.Where(x => x.{{m2mWithManyProperty_1.Name}}.Id == selected{{entity.Name}}DTO.{{m2mWithManyProperty_1.Name}}Id).SingleOrDefault();

                    if ({{entity.Name.FirstCharToLower()}} == null)
                    {
                        {{entity.Name.FirstCharToLower()}} = TypeAdapter.Adapt<{{entity.Name}}>(selected{{entity.Name}}DTO, Mapper.{{entity.Name}}DTOToEntityConfig());
                        {{entity.Name.FirstCharToLower()}}.{{m2mWithManyProperty_2.Name}} = await GetInstanceAsync<{{m2mWithManyProperty_2.Type}}, {{m2mEntityIdType_2}}>({{m2mWithManyProperty_2.Name.FirstCharToLower()}}Id, null);
                        {{entity.Name.FirstCharToLower()}}.{{m2mWithManyProperty_1.Name}} = await GetInstanceAsync<{{m2mWithManyProperty_1.Type}}, {{m2mEntityIdType_1}}>(selected{{entity.Name}}DTO.{{m2mWithManyProperty_1.Name}}Id.Value, null);
                        dbSet.Add({{entity.Name.FirstCharToLower()}});
                    }
                    else
                    {
                        selected{{entity.Name}}DTO.Adapt({{entity.Name.FirstCharToLower()}}, Mapper.{{entity.Name}}DTOToEntityConfig());
                        dbSet.Update({{entity.Name.FirstCharToLower()}});

                        {{entity.Name.FirstCharToLower()}}List.Remove({{entity.Name.FirstCharToLower()}});
                    }
                }

                dbSet.RemoveRange({{entity.Name.FirstCharToLower()}}List);

                await _deps.Context.SaveChangesAsync();
            });
        }
""";
        }
    }
}
