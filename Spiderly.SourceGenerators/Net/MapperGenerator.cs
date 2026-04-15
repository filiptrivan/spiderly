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
    public class MapperGenerator : IIncrementalGenerator
    {

        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            var combined = PipelineFactory.CreatePipeline(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DataMappers },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DataMappers });

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var ((classes, referencedClasses), config) = source;
                Execute(classes, referencedClasses, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(MapperGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();

            SpiderlyClass customMapperClass = Helpers.GetManualyWrittenMapperClass(currentProjectClasses);

            StringBuilder sb = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

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
                string entityRegion;
                try
                {
                    entityRegion = $$"""

        #region {{entity.Name}}

{{(entity.IsAbstract ? "" : GetMapperToEntity($"{entity.Name}DTOToEntityConfig", customMapperClass, entity, currentProjectEntities))}}

{{GetMapToDTO($"{entity.Name}ToDTOConfig", customMapperClass, entity, currentProjectEntities)}}

{{GetProjectToDTO($"{entity.Name}ProjectToConfig", customMapperClass, entity, currentProjectEntities)}}

{{GetExcelProjectToDTO($"{entity.Name}ExcelProjectToConfig", customMapperClass, entity, currentProjectEntities)}}

        #endregion

""";
                }
                catch (SpiderlyGenerationException ex)
                {
                    context.ReportDiagnostic(ex.Diagnostic);
                    continue;
                }

                sb.AppendLine(entityRegion);
            }

            sb.AppendLine($$"""
    }
}
""");

            context.AddSource($"Mapper.generated", SourceText.From(sb.ToString(), Encoding.UTF8));
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
            TypeAdapterConfig config = new();

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
            TypeAdapterConfig config = new();

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
            ForeignKeyValidator.ValidateEntity(entity, entities);

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

                    string manyToOneEntityDisplayName = ClassAnalyzer.GetDisplayNameProperty(manyToOneEntity);
                    manyToOneEntityDisplayName = manyToOneEntityDisplayName.Replace(".ToString()", ""); // TODO FT: Check why are you doing this, maybe it's okay to do ToString()

                    // Explicit FK (declared scalar) → read the scalar directly; avoids EF Core's spurious
                    // JOIN on `src.Nav.Id` (unresolved since 2019: https://github.com/dotnet/efcore/issues/15826).
                    // Shadow FK fallback → `src.Nav.Id`. Mapster inserts null-checks for nested access, and
                    // EF.Property<>() cannot be used here because the same mapper config runs outside EF
                    // (Mapster.Adapt on materialized entities), where EF.Property throws at runtime.
                    string fkName = property.ResolveExplicitForeignKeyName(entity);
                    if (fkName != null)
                    {
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{fkName}, src => src.{fkName})");
                    }
                    else
                    {
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}Id, src => src.{property.Name}.Id)");
                    }
                    manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}DisplayName, src => src.{property.Name}.{manyToOneEntityDisplayName})"); // "dest.TierDisplayName", "src.Tier.Name"
                }

                if (property.Type.IsOneToManyType())
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    if (extractedEntity == null)
                        continue;

                    string extractedEntityDisplayName = ClassAnalyzer.GetDisplayNameProperty(extractedEntity);
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
