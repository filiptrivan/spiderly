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
    /// Generates Mapster configuration methods (`{YourAppName}Mapper.generated.cs`)
    /// within the `{YourBaseNamespace}.DataMappers` namespace. This generator automates
    /// the creation of mapping configurations between your entities and DTOs using the Mapster library.
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
