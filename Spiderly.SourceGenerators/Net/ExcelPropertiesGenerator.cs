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

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates a static class `ExcelPropertiesToExclude` (`ExcelPropertiesToExclude.generated.cs`)
    /// within the `{YourBaseNamespace}.ExcelProperties` namespace. This class provides methods
    /// to define which properties of your DTOs should be excluded during Excel export operations.
    /// </summary>
    [Generator]
    public class ExcelPropertiesGenerator : IIncrementalGenerator
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO, ClassCategoryCodes.DataMappers },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var (((classes, referencedClasses), config), nullableContext) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(ExcelPropertiesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectClasses, allClasses);

            StringBuilder sb = new();

            string namespaceValue = currentProjectClasses[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            sb.AppendLine($$"""
{{string.Join("\n", ReferencedAssemblyAnalyzer.GetClassesUsings(currentProjectDTOClasses))}}

namespace {{basePartOfNamespace}}.ExcelProperties
{
    public static class ExcelPropertiesToExclude
    {
""");
            foreach (IGrouping<string, SpiderlyClass> DTOClassGroup in currentProjectDTOClasses.GroupBy(x => x.Name))
            {
                sb.AppendLine($$"""
        public static string[] GetHeadersToExclude({{DTOClassGroup.Key}} _)
        {
""");

                List<SpiderlyProperty> DTOProperties = new();
                foreach (SpiderlyClass DTOClass in DTOClassGroup)
                    DTOProperties.AddRange(DTOClass.Properties);

                List<string> propertyNames = new();

                foreach (string propertyName in GetPropertiesToExcludeFromExcelExport(DTOClassGroup.Key, DTOProperties, allClasses))
                    propertyNames.Add($"\"{propertyName}\"");

                sb.AppendLine($$"""
            return new string[] { {{string.Join(", ", propertyNames)}} };
        }
""");
            }
            sb.AppendLine($$"""
    }
}
""");

            context.AddSpiderlyCSharpSource("ExcelPropertiesToExclude.generated", sb.ToString(), nullableContext);
        }

        private static List<string> GetPropertiesToExcludeFromExcelExport(string DTOClassName, List<SpiderlyProperty> DTOProperties, List<SpiderlyClass> allClasses)
        {
            // Always exclude enumerables (collections) from the Excel export — a flat sheet can't
            // render nested lists. This already covers [IncludeInDTO] O2M "{Nav}DTOList" columns,
            // so they need no separate handling in the attribute pass below.
            List<string> DTOClassPropertiesToExclude = DTOProperties
                .Where(prop => prop.Type.IsEnumerable())
                .Select(x => x.Name)
                .ToList();

            // Honor [ExcludeFromExcelExport] declared on the source entity. The generated DTO
            // properties don't carry entity attributes, so resolve the entity for this DTO and map
            // each attributed entity property to the DTO column name(s) it produces — via the same
            // SpiderlyClassFactory.GetDTOColumns the DTO generator uses, so the exclusion can never
            // drift from the real generated column names.
            SpiderlyClass entity = GetEntityForDTO(DTOClassName, allClasses);

            if (entity != null)
            {
                foreach (SpiderlyProperty property in entity.Properties)
                {
                    // Gate exactly as GetSpiderlyDTOProperties does, so we only ever name columns the
                    // DTO actually has. Skips properties absent from the DTO ([ExcludeFromDTO], plain
                    // O2M navs) — but NOT [UIDoNotGenerate] scalars, which stay in the DTO and are the
                    // common reason to reach for [ExcludeFromExcelExport].
                    if (property.ShouldSkipPropertyInDTO())
                        continue;

                    if (!property.Attributes.Any(x => x.Name == "ExcludeFromExcelExport"))
                        continue;

                    foreach (SpiderlyDTOColumn column in SpiderlyClassFactory.GetDTOColumns(property, entity, allClasses))
                        DTOClassPropertiesToExclude.Add(column.Name);
                }
            }

            // Hand-written ([SpiderlyDTO]) DTOs aren't derived from an entity, so their own
            // properties carry the attribute directly — honor it there too. (Entity-derived DTO
            // properties have empty Attributes, so this loop is a no-op for them.)
            foreach (SpiderlyProperty dtoProperty in DTOProperties)
            {
                if (dtoProperty.Attributes.Any(x => x.Name == "ExcludeFromExcelExport"))
                    DTOClassPropertiesToExclude.Add(dtoProperty.Name);
            }

            return DTOClassPropertiesToExclude.Distinct().ToList();
        }

        /// <summary>
        /// Resolves the source entity for a generated DTO class name by matching it against the
        /// DTO names the entity produces ("{Entity}DTO", "{Entity}SaveBodyDTO",
        /// "{Entity}MainUIFormDTO"). Returns null for hand-written DTOs with no matching entity.
        /// Matching the full generated name (rather than stripping a suffix) avoids mis-resolving
        /// entities whose own name ends in "SaveBody"/"MainUIForm".
        /// </summary>
        private static SpiderlyClass GetEntityForDTO(string DTOClassName, List<SpiderlyClass> allClasses)
        {
            return allClasses.FirstOrDefault(x =>
                x.HasSpiderlyEntityAttribute() && SpiderlyNaming.IsGeneratedDTOName(DTOClassName, x.Name));
        }
    }
}