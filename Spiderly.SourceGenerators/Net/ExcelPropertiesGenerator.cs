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
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(ExcelPropertiesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectClasses, allClasses);

            SpiderlyClass customMapperClass = Helpers.GetManualyWrittenMapperClass(currentProjectClasses);

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

                foreach (string propertyName in GetPropertiesToExcludeFromExcelExport(DTOClassGroup.Key, DTOProperties, customMapperClass))
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

            context.AddSource("ExcelPropertiesToExclude.generated", SourceText.From(sb.ToString(), Encoding.UTF8));
        }

        private static List<string> GetPropertiesToExcludeFromExcelExport(string DTOClassName, List<SpiderlyProperty> DTOProperties, SpiderlyClass customMapperClass)
        {
            List<string> DTOClassPropertiesToExclude = new();

            SpiderlyMethod excelMethod = customMapperClass.Methods
                .SingleOrDefault(x => x.ReturnType == DTOClassName && x.Name == "ExcelProjectTo");

            List<SpiderlyAttribute> excludePropertyAttributes = new();

            DTOClassPropertiesToExclude = DTOProperties // FT: Excluding Enumerables from the excel
                .Where(prop => prop.Type.IsEnumerable())
                .Select(x => x.Name)
                .ToList();

            if (excelMethod != null)
            {
                foreach (SpiderlyAttribute attribute in excelMethod.Attributes)
                {
                    if (attribute.Name == "MapperIgnoreTarget")
                    {
                        DTOClassPropertiesToExclude.Add(attribute.Value);
                    }
                }
            }

            return DTOClassPropertiesToExclude;
        }
    }
}