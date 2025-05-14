using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// **Summary:**
    /// Generates Mapster configuration methods (`{YourProjectName}Mapper.generated.cs`)
    /// within the `{YourBaseNamespace}.DataMappers` namespace. This generator automates
    /// the creation of mapping configurations between your entities and DTOs using the Mapster library.
    ///
    /// **Key Features:**
    /// - **Automatic Mapping Configuration:** For each entity in your project (within the '.Entities' namespace),
    ///   it generates Mapster `TypeAdapterConfig` methods for mapping:
    ///     - From DTO to Entity (`{EntityName}DTOToEntityConfig`).
    ///     - From Entity to DTO (`{EntityName}ToDTOConfig`).
    ///     - Projecting Entity to DTO (with optional custom mappings via `[ProjectToDTO]` attribute) (`{EntityName}ProjectToConfig`).
    ///     - Projecting Entity to DTO for Excel export (`{EntityName}ExcelProjectToConfig`).
    /// - **Many-to-One Relationship Handling:** Automatically configures mapping for properties representing many-to-one relationships, mapping the related entity's ID and a display name property (identified by convention or a `DisplayName` attribute).
    /// - **One-to-Many Relationship Handling:** Provides an option (using the `[GenerateCommaSeparatedDisplayName]` attribute on the one-to-many property) to map a comma-separated string of display names from the related entities to a property in the DTO.
    /// - **Custom Mapping Support:** Respects manually written mapping methods in your custom mapper class (typically named `Mapper` in the `.DataMappers` namespace) and avoids generating duplicate configurations.
    /// - **Extensibility:** Generates partial `Mapper` class, allowing you to add your own custom mapping configurations and extension methods.
    ///
    /// **How to Use:**
    /// 1. Ensure your entity classes are located in a namespace ending with `.Entities`.
    /// 2. Create corresponding DTO classes in a namespace ending with `.DTO`.
    /// 3. (Optional) Create a partial static class named `Mapper` in a namespace ending with `.DataMappers` if you need to add custom mapping logic or use the `[MapperIgnoreTarget]` attribute (for Excel export exclusion).
    /// 4. (Optional) On your entity classes, you can use the `[ProjectToDTO("CustomMapping")]` attribute to specify the name of a custom mapping method within your `Mapper` class to be included in the projection to the DTO.
    /// 5. (Optional) On one-to-many navigation properties in your entity, you can use the `[GenerateCommaSeparatedDisplayName]` attribute to generate a comma-separated string of display names in the DTO. Ensure the related entity has a property identified as its display name.
    /// 6. Build your .NET project. This source generator will automatically create the `{YourProjectName}Mapper.generated.cs` file.
    /// 7. In your application, you can use the generated `TypeAdapterConfig` methods with Mapster's `Adapt()` and `ProjectToType()` extensions to perform mapping between entities and DTOs.
    ///
    /// **Generated Output:**
    /// - `{YourProjectName}Mapper.generated.cs`: Contains a partial static class `Mapper` with methods like:
    ///     - `EntityDTOToEntityConfig()`
    ///     - `EntityToDTOConfig()`
    ///     - `EntityProjectToConfig()` (includes mappings for many-to-one relationships and custom projections)
    ///     - `EntityExcelProjectToConfig()` (similar to `ProjectToDTOConfig`)
    /// - The namespace will be `{YourBaseNamespace}.DataMappers`.
    /// - Includes necessary `using` statements for Mapster, ASP.NET Core, and your DTO and Entity namespaces.
    ///
    /// **Dependencies:**
    /// - Requires the Mapster library to be installed in your project.
    /// - Assumes a consistent project structure with Entities and DTOs namespaces.
    /// 
    /// </summary>
    [Generator]
    public class MapperlyGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DataMappers,
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DataMappers,
                });

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, SourceProductionContext context)
        {
            if (classes.Count <= 1) return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();

            SpiderlyClass customMapperClass = Helpers.GetManualyWrittenMapperClass(currentProjectClasses);

            StringBuilder sb = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            sb.AppendLine($$"""
using Mapster;
using Microsoft.AspNetCore.Http;
using {{basePartOfNamespace}}.DTO;
using {{basePartOfNamespace}}.Entities;

namespace {{basePartOfNamespace}}.DataMappers
{
    public static partial class Mapper
    {
""");
            foreach (SpiderlyClass entity in currentProjectEntities)
            {
                sb.AppendLine($$"""

        #region {{entity.Name}}

{{(entity.IsAbstract ? "" : GetMapperToEntity($"{entity.Name}DTOToEntityConfig", customMapperClass, entity, currentProjectEntities))}}

{{GetMapToDTO($"{entity.Name}ToDTOConfig", customMapperClass, entity, currentProjectEntities)}}

{{GetProjectToDTO($"{entity.Name}ProjectToConfig", customMapperClass, entity, currentProjectEntities)}}

{{GetExcelProjectToDTO($"{entity.Name}ExcelProjectToConfig", customMapperClass, entity, currentProjectEntities)}}

        #endregion

""");
            }

            sb.AppendLine($$"""
    }
}
""");

            context.AddSource($"{projectName}Mapper.generated", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        #region To Entity

        public static string GetMapperToEntity(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            if (customMapperClass == null)
                return "You didn't define DataMappers";

            if (HasCustomPair(customMapperClass, methodName))
                return "";

            List<string> mappers = GetFromDTOToEntityConfig(entity, entities);

            string result = $$"""
        public static TypeAdapterConfig {{methodName}}()
        {
            TypeAdapterConfig config = new TypeAdapterConfig();

            config
                .NewConfig<{{entity.Name}}DTO, {{entity.Name}}>()
{{string.Join("\n", mappers)}}
                ;

            return config;
        }
""";

            return result;
        }

        private static List<string> GetFromDTOToEntityConfig(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                // FT: This was the code if we store images in our database, delete if you don't need
//                if (property.Type == "byte[]")
//                    result.Add($$"""
//                .Map(dest => dest.{{property.Name}}, src => src.{{property.Name}} == null ? null : Convert.FromBase64String(src.{{property.Name}}))
//""");
            }

            return result;
        }

        #endregion

        #region To DTO

        public static string GetMapToDTO(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<SpiderlyClass> currentProjectEntities)
        {
            return GetToDTOConfig(methodName, customMapperClass, entity, currentProjectEntities, customMappers: []);
        }

        private static string GetProjectToDTO(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<SpiderlyClass> currentProjectEntities)
        {
            List<string> customMappers = new();

            foreach (SpiderlyAttribute attribute in entity.Attributes.Where(x => x.Name == "ProjectToDTO"))
            {
                customMappers.Add(attribute.Value);
            }

            return GetToDTOConfig(methodName, customMapperClass, entity, currentProjectEntities, customMappers);
        }

        private static string GetExcelProjectToDTO(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<SpiderlyClass> currentProjectEntities)
        {
            return GetToDTOConfig(methodName, customMapperClass, entity, currentProjectEntities, customMappers: []);
        }

        public static string GetToDTOConfig(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<SpiderlyClass> currentProjectEntities, List<string> customMappers)
        {
            if (customMapperClass == null)
                return "You didn't define DataMappers";

            if (HasCustomPair(customMapperClass, methodName))
                return "";

            List<string> manyToOneMappers = GetConfigForManyToOneClass(entity, currentProjectEntities);

            foreach (string manyToOneMapper in manyToOneMappers)
            {
                customMappers.Add(manyToOneMapper);
            }

            return $$"""
        public static TypeAdapterConfig {{methodName}}()
        {
            TypeAdapterConfig config = new TypeAdapterConfig();

            config
                .NewConfig<{{entity.Name}}, {{entity.Name}}DTO>()
                {{string.Join("\n\t\t\t\t", customMappers)}}
                ;

            return config;
        }
""";
        }

        public static List<string> GetConfigForManyToOneClass(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> manyToOneAttributeMappers = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.Type.IsManyToOneType())
                {
                    SpiderlyClass manyToOneEntity = entities
                        .Where(x => x.Name == property.Type)
                        .SingleOrDefault();

                    if (manyToOneEntity == null)
                        continue;

                    string manyToOneEntityDisplayName = Helpers.GetDisplayNameProperty(manyToOneEntity);
                    manyToOneEntityDisplayName = manyToOneEntityDisplayName.Replace(".ToString()", ""); // TODO FT: Check why are you doing this, maybe it's okay to do ToString()

                    manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}Id, src => src.{property.Name}.Id)"); // "dest.TierId", "src.Tier.Id"
                    manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}DisplayName, src => src.{property.Name}.{manyToOneEntityDisplayName})"); // "dest.TierDisplayName", "src.Tier.Name"
                }

                if (property.Type.IsOneToManyType())
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                    if (extractedEntity == null)
                        continue;

                    string extractedEntityDisplayName = Helpers.GetDisplayNameProperty(extractedEntity);
                    extractedEntityDisplayName = extractedEntityDisplayName.Replace(".ToString()", ""); 

                    if (property.HasGenerateCommaSeparatedDisplayNameAttribute())
                    {
                        // FT: eg. ".Map(dest => dest.SegmentationItemsCommaSeparated, src => string.Join(", ", src.CheckedSegmentationItems.Select(x => x.Name)))"
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}CommaSeparated, src => string.Join(\", \", src.{property.Name}.Select(x => x.{extractedEntityDisplayName})))");
                    }
                }
            }

            return manyToOneAttributeMappers;
        }

        #endregion

        #region Helpers

        private static bool HasCustomPair(SpiderlyClass customMapperClass, string methodName)
        {
            if (customMapperClass.Methods.Any(x => x.Name == methodName))
                return true;

            return false;
        }

        #endregion
    }
}
