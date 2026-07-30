using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.Collections.Immutable;
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

            var combinedWithEnums = combined
                .Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider))
                .Combine(PipelineFactory.GetNullableContextProvider(context));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var ((combinedSource, enumNames), nullableContext) = source;
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(MapperGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectClasses, allClasses);

            SpiderlyClass customMapperClass = Helpers.GetManualyWrittenMapperClass(currentProjectClasses);

            // The pipeline also collects data mappers, so `classes` can be non-empty in a project that
            // declares no entities — nothing to map there.
            if (currentProjectEntities.Count == 0)
                return;

            StringBuilder sb = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            sb.AppendLine($$"""
using Mapster;
using Microsoft.AspNetCore.Http;
{{string.Join("\n", ReferencedAssemblyAnalyzer.GetClassesUsings(currentProjectDTOClasses))}}
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
                    // Entity-shape validation is NOT here: it runs in EntityValidationGenerator, which no
                    // .spiderly/config.json toggle can switch off. The catch below stays — emission itself
                    // can still fault on one entity, and the rest must keep generating.
                    // Once per entity: the three ToDTO configs differ only by method name, so recomputing an
                    // identical mapper list for each was O(props x entities) work done three times over. Safe
                    // to collapse only now that the entity validators no longer live inside this helper —
                    // their 3x invocation was what kept the HasCustomPair gap shut.
                    List<string> manyToOneMappers = GetConfigForManyToOneClass(entity, currentProjectEntities);

                    entityRegion = $$"""

        #region {{entity.Name}}

{{(entity.IsAbstract ? "" : GetMapperToEntity($"{entity.Name}DTOToEntityConfig", customMapperClass, entity, currentProjectEntities))}}

{{GetToDTOConfig($"{entity.Name}ToDTOConfig", customMapperClass, entity, manyToOneMappers)}}

{{GetToDTOConfig($"{entity.Name}ProjectToConfig", customMapperClass, entity, manyToOneMappers)}}

{{GetToDTOConfig($"{entity.Name}ExcelProjectToConfig", customMapperClass, entity, manyToOneMappers)}}

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

        /// <summary>
        /// A Mapster config with the convention-FLATTENING member strategy stripped: an unmapped
        /// DTO property stays at its default instead of silently resolving through a same-named
        /// navigation chain (e.g. dest.ShippingTierIsBulky -> src.ShippingTier.IsBulky), where an
        /// optional navigation's LEFT JOIN NULL crashes EF's shaper on a non-nullable member.
        /// Deliberate custom mappings go through the Customize* partial hooks instead.
        /// </summary>
        private static TypeAdapterConfig NewStrictConfig()
        {
            TypeAdapterConfig config = new();

            foreach (TypeAdapterRule rule in config.Rules)
                rule.Settings.ValueAccessingStrategies.Remove(ValueAccessingStrategy.FlattenMember);

            return config;
        }
    }
}
""");

            context.AddSpiderlyCSharpSource("Mapper.generated", sb.ToString(), nullableContext);
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
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<{{entity.Name}}DTO, {{entity.Name}}>()
{{string.Join("\n", mappers)}}
                ;

            Customize{{methodName}}(config);

            return config;
        }

{{GetCustomizeHookDeclaration(methodName)}}
""";

            return result;
        }

        private static List<string> GetFromDTOToEntityConfig(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                // [ReadOnly] is server-owned: the read DTO keeps the property (still nested in the
                // SaveBody payload), so Mapster would map it onto the entity by name convention and a
                // crafted payload could overwrite a backend-managed value. .Ignore() closes that path.
                // Scoped to scalars — navs/collections don't flow through a same-named dest here.
                if (property.HasReadOnlyAttribute() &&
                    property.Type.IsEnumerable() == false &&
                    property.IsForeignKeyReferenceNav() == false)
                {
                    result.Add($"                .Ignore(dest => dest.{property.Name})");
                }
            }

            return result;
        }

        #endregion

        #region To DTO

        public static string GetToDTOConfig(string methodName, SpiderlyClass customMapperClass, SpiderlyClass entity, List<string> manyToOneMappers)
        {
            if (customMapperClass == null)
                return "You didn't define DataMappers";

            if (HasCustomPair(customMapperClass, methodName))
                return "";

            return $$"""
        public static TypeAdapterConfig {{methodName}}()
        {
            TypeAdapterConfig config = NewStrictConfig();

            config
                .NewConfig<{{entity.Name}}, {{entity.Name}}DTO>()
                {{string.Join("\n\t\t\t\t", manyToOneMappers)}}
                ;

            Customize{{methodName}}(config);

            return config;
        }

{{GetCustomizeHookDeclaration(methodName)}}
""";
        }

        public static List<string> GetConfigForManyToOneClass(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> manyToOneAttributeMappers = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                // The principal side of a 1-1 has no FK/DisplayName columns on its own DTO (the FK lives on
                // the dependent), so it gets no Mapster .Map(...) here — skip it before the M2O branch.
                if (property.IsOneToOnePrincipalInverse(entity, entities))
                    continue;

                // The principal inverse was skipped above; this branch maps M2O navs and the 1-1 dependent.
                if (property.IsForeignKeyReferenceNav())
                {
                    SpiderlyClass manyToOneEntity = entities
                        .Where(x => x.Name == property.Type.Name)
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
                    string? fkName = property.ResolveExplicitForeignKeyName(entity);
                    if (fkName != null)
                    {
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{fkName}, src => src.{fkName})");
                    }
                    else
                    {
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}Id, src => src.{property.Name}!.Id)");
                    }
                    // The whole path goes through the helper, nav included — the nav is just the first
                    // intermediate segment, and [DisplayName("Project.Name")] means the tail can walk one too.
                    // Both consumers handle a null chain: Mapster null-checks nested access (see above), EF
                    // Core LEFT JOINs it. See Extensions.AsNullForgivingProjection.
                    manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}DisplayName, src => src.{$"{property.Name}.{manyToOneEntityDisplayName}".AsNullForgivingProjection()})"); // "dest.TierDisplayName", "src.Tier!.Name"
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
                        manyToOneAttributeMappers.Add($".Map(dest => dest.{property.Name}CommaSeparated, src => string.Join(\", \", src.{property.Name}.Select(x => x.{extractedEntityDisplayName.AsNullForgivingProjection()})))");
                    }
                }
            }

            return manyToOneAttributeMappers;
        }

        #endregion

        #region Helpers

        private static string GetCustomizeHookDeclaration(string methodName)
        {
            return $$"""
        /// <summary>Optional extension seam for {{methodName}} — implement in your hand-written Mapper partial to add custom mappings (see the mapper-customization docs; null-guard optional navigations).</summary>
        static partial void Customize{{methodName}}(TypeAdapterConfig config);
""";
        }

        private static bool HasCustomPair(SpiderlyClass customMapperClass, string methodName)
        {
            if (customMapperClass.Methods.Any(x => x.Name == methodName))
                return true;

            return false;
        }

        #endregion
    }
}
