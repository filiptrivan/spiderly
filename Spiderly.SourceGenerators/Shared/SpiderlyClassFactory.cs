using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Shared
{
    public static class SpiderlyClassFactory
    {
        public static List<SpiderlyClass> GetSpiderlyClasses(IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> referencedProjectsClasses)
            => GetSpiderlyClasses(currentProjectClasses, referencedProjectsClasses, ImmutableArray<string>.Empty);

        /// <summary>
        /// Enum-aware overload. <paramref name="spiderlyEnumNames"/> is the set of <c>[SpiderlyEnum]</c>-decorated enum type names
        /// in the current compilation; entity properties whose stringified type matches a name in the set get <c>IsEnum = true</c>.
        /// Pass <see cref="ImmutableArray{T}.Empty"/> to opt out of enum tagging (legacy behavior).
        /// </summary>
        public static List<SpiderlyClass> GetSpiderlyClasses(IList<ClassDeclarationSyntax> currentProjectClasses, List<SpiderlyClass> referencedProjectsClasses, ImmutableArray<string> spiderlyEnumNames)
        {
            return currentProjectClasses
                .Select(x =>
                {
                    return new SpiderlyClass
                    {
                        Name = x.Identifier.Text,
                        Namespace = x.Ancestors()
                            .OfType<NamespaceDeclarationSyntax>()
                            .FirstOrDefault()?.Name.ToString(),
                        BaseType = x.GetBaseType(),
                        IsAbstract = x.IsAbstract(),
                        Description = ClassAnalyzer.GetXmlDocSummary(x),
                        Properties = ClassAnalyzer.GetAllPropertiesOfTheClass(x, currentProjectClasses, referencedProjectsClasses, spiderlyEnumNames),
                        Attributes = ClassAnalyzer.GetAllAttributesOfTheClass(x, currentProjectClasses, referencedProjectsClasses),
                        Methods = ClassAnalyzer.GetMethodsOfCurrentClass(x),
                        Location = x.Identifier.GetLocation(),
                    };
                })
                .OrderBy(x => x.Name)
                .ToList();
        }

        #region DTO

        public static List<SpiderlyClass> GetDTOClasses(List<SpiderlyClass> currentProjectClasses, List<SpiderlyClass> allClasses)
        {
            List<SpiderlyClass> DTOList = new();

            foreach (var x in currentProjectClasses)
            {
                if (x.HasSpiderlyDTOAttribute())
                {
                    DTOList.Add(new SpiderlyClass
                    {
                        Name = x.Name,
                        Properties = x.Properties,
                        Attributes = x.Attributes,
                        BaseType = x.BaseType,
                        IsAbstract = x.IsAbstract,
                        Methods = x.Methods,
                        Namespace = x.Namespace,
                        Location = x.Location,
                        IsGenerated = false,
                    });
                }
                else if (x.HasSpiderlyEntityAttribute())
                {
                    string dtoNamespace = GetDtoNamespaceForEntity(x.Namespace);

                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}DTO",
                        BaseType = x.GetDTOBaseType(),
                        Description = x.Description,
                        Properties = GetSpiderlyDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}SaveBodyDTO",
                        Properties = GetSaveBodyDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                    DTOList.Add(new SpiderlyClass
                    {
                        Name = $"{x.Name}MainUIFormDTO",
                        Properties = GetMainUIFormDTOProperties(x, allClasses),
                        Namespace = dtoNamespace,
                        Location = x.Location,
                        IsGenerated = true
                    });
                }
            }

            return DTOList;
        }

        private static string GetDtoNamespaceForEntity(string entityNamespace)
        {
            string entitiesSuffix = $".{Helpers.EntitiesNamespaceEnding}";
            string dtoSuffix = $".{Helpers.DTONamespaceEnding}";

            return entityNamespace.EndsWith(entitiesSuffix)
                ? entityNamespace.Substring(0, entityNamespace.Length - entitiesSuffix.Length) + dtoSuffix
                : entityNamespace;
        }

        private static List<SpiderlyProperty> GetSaveBodyDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> result = new();

            result.Add(new SpiderlyProperty { Name = $"{entity.Name}DTO", Type = $"{entity.Name}DTO", EntityName = $"{entity.Name}SaveBodyDTO", IsSaveBodyMainDTO = true });

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.HasUIOrderedOneToManyAttribute() ||
                    x.IsMultiSelectControlType() ||
                    x.IsMultiAutocompleteControlType() ||
                    x.HasSimpleManyToManyTableLazyLoadAttribute() ||
                    x.HasComplexManyToManyListAttribute()
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
                string extractedEntityIdType = extractedEntity.GetIdType(entities);


                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyProperty orderedProp = new SpiderlyProperty { Name = $"Ordered{property.Name}SaveBodyDTO", Type = $"List<{extractedEntity.Name}SaveBodyDTO>", EntityName = $"{entity.Name}SaveBodyDTO" };

                    if (property.Attributes.Any(x => x.Name == "Required"))
                        orderedProp.Attributes.Add(new SpiderlyAttribute { Name = "Required" });

                    result.Add(orderedProp);
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}NamebookDTOList", Type = $"List<NamebookDTO<{extractedEntityIdType}>>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = $"Selected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"Unselected{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"AreAll{property.Name}Selected", Type = "bool?", EntityName = $"{entity.Name}SaveBodyDTO" });
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}TableFilter", Type = "FilterDTO", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
                else if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = property.Name, Type = $"List<{extractedEntity.Name}DTO>", EntityName = $"{entity.Name}SaveBodyDTO" });
                }
            }

            return result;
        }

        private static List<SpiderlyProperty> GetMainUIFormDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> result = new();

            result.Add(new SpiderlyProperty { Name = $"{entity.Name}DTO", Type = $"{entity.Name}DTO", EntityName = $"{entity.Name}MainUIFormDTO" });

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.HasUIOrderedOneToManyAttribute() ||
                    x.IsMultiSelectControlType() ||
                    x.IsMultiAutocompleteControlType() ||
                    x.HasComplexManyToManyListAttribute()
                )
            )
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
                string extractedEntityIdType = extractedEntity.GetIdType(entities);

                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyProperty orderedProp = new SpiderlyProperty { Name = $"Ordered{property.Name}MainUIFormDTO", Type = $"List<{extractedEntity.Name}MainUIFormDTO>", EntityName = $"{entity.Name}MainUIFormDTO" };

                    if (property.Attributes.Any(x => x.Name == "Required"))
                        orderedProp.Attributes.Add(new SpiderlyAttribute { Name = "Required" });

                    result.Add(orderedProp);
                }
                else if (property.IsMultiSelectControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}Ids", Type = $"List<{extractedEntityIdType}>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add(new SpiderlyProperty { Name = $"{property.Name}NamebookDTOList", Type = $"List<NamebookDTO<{extractedEntityIdType}>>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
                else if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add(new SpiderlyProperty { Name = property.Name, Type = $"List<{extractedEntity.Name}DTO>", EntityName = $"{entity.Name}MainUIFormDTO" });
                }
            }

            return result;
        }

        public static List<SpiderlyProperty> GetSpiderlyDTOProperties(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> DTOProperties = new(); // public string Email { get; set; }

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.ShouldSkipPropertyInDTO())
                    continue;

                if (property.IsManyToOneType())
                {
                    SpiderlyClass manyToOneClass = entities.SingleOrDefault(x => x.Name == property.Type.Raw);

                    DTOProperties.Add(new SpiderlyProperty { Name = $"{property.Name}DisplayName", Type = "string", EntityName = $"{property.EntityName}DTO" });

                    // Skip FK synthesis when an explicit FK scalar is declared on the entity —
                    // the scalar flows through the standard `else` branch below under its real name
                    // (which may be {NavName}Id by convention, or a renamed property via [ForeignKey]).
                    if (property.ResolveExplicitForeignKeyName(entity) == null)
                        DTOProperties.Add(new SpiderlyProperty { Name = $"{property.Name}Id", Type = $"{manyToOneClass.GetIdType(entities)}?", EntityName = $"{property.EntityName}DTO" });
                }
                else if (property.Type.IsOneToManyType() && property.HasGenerateCommaSeparatedDisplayNameAttribute())
                {
                    DTOProperties.Add(new SpiderlyProperty { Name = $"{property.Name}CommaSeparated", Type = "string", EntityName = $"{property.EntityName}DTO" });
                }
                else if (property.Type.IsOneToManyType() && property.HasIncludeInDTOAttribute())
                {
                    DTOProperties.Add(new SpiderlyProperty { Name = $"{property.Name}DTOList", Type = property.Type.Raw.Replace(">", "DTO>"), EntityName = $"{property.EntityName}DTO" });
                }
                else if (property.IsBlob())
                {
                    DTOProperties.Add(new SpiderlyProperty { Name = $"{property.Name}Data", Type = "string", EntityName = $"{property.EntityName}DTO" });
                    DTOProperties.Add(new SpiderlyProperty { Name = property.Name, Type = "string", EntityName = $"{property.EntityName}DTO" });
                }
                else
                {
                    DTOProperties.Add(new SpiderlyProperty { Name = property.Name, Type = GetFormatedDTOPropertyType(property.Type.Raw), EntityName = $"{property.EntityName}DTO", Description = property.Description, IsEnum = property.IsEnum });
                }
            }

            return DTOProperties;
        }

        public static string GetFormatedDTOPropertyType(string propertyType)
        {
            if (propertyType != "string" && propertyType.IsBaseDataType())
                return $"{propertyType}?".Replace("??", "?");

            return propertyType;
        }

        #endregion
    }
}
