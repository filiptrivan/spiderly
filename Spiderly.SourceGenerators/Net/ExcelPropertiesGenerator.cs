using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
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
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                    NamespaceExtensionCodes.DataMappers,
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                });

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) => Execute(source.Left, source.Right, spc));
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, SourceProductionContext context)
        {
            if (classes.Count <= 1) 
                return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> allClasses = currentProjectClasses.Concat(referencedProjectClasses).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = Helpers.GetDTOClasses(currentProjectClasses, allClasses);

            SpiderlyClass customMapperClass = Helpers.GetManualyWrittenMapperClass(currentProjectClasses);

            StringBuilder sb = new();

            string namespaceValue = currentProjectClasses[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            sb.AppendLine($$"""
using Spiderly.Shared.Excel.DTO;
using {{basePartOfNamespace}}.DTO;

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

            SpiderMethod excelMethod = customMapperClass.Methods
                .Where(x => x.ReturnType == DTOClassName && x.Name == "ExcelProjectTo")
                .SingleOrDefault();

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