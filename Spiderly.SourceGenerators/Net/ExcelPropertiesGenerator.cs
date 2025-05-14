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
    /// **Summary:**
    /// Generates a static class `ExcelPropertiesToExclude` (`ExcelPropertiesToExclude.generated.cs`)
    /// within the `{YourBaseNamespace}.ExcelProperties` namespace. This class provides methods
    /// to define which properties of your DTOs should be excluded during Excel export operations.
    ///
    /// **Key Features:**
    /// - **Automatic Exclusion for Enumerables:** By default, any property in your DTO that is an enumerable type (e.g., `List<T>`, `IEnumerable<T>`) will be automatically marked for exclusion from Excel exports.
    /// - **Custom Exclusion via Mapper Attribute:** Allows you to explicitly specify properties to exclude from Excel export by using the `[MapperIgnoreTarget]` attribute within a custom mapper method named `ExcelProjectTo` that returns the specific DTO type.
    /// - **DTO-Specific Configuration:** Generates a `GetHeadersToExclude` method for each of your DTO classes, making it easy to define exclusion rules on a per-DTO basis.
    ///
    /// **How to Use:**
    /// 1. Ensure your DTO classes are located in a namespace ending with `.DTO`.
    /// 2. If you need to exclude enumerable properties, no further action is required; they will be excluded by default.
    /// 3. For more specific exclusions, create a manually written mapper class (typically in a namespace ending with `.DataMappers`).
    /// 4. Within this mapper class, define a method named `ExcelProjectTo` that takes an entity (or any relevant source) and returns the DTO you want to configure for Excel export.
    /// 5. On the properties within this `ExcelProjectTo` method's body that you want to exclude from Excel export, apply the `[MapperIgnoreTarget("PropertyName")]` attribute, where "PropertyName" is the name of the property in the target DTO.
    /// 6. Build your .NET project. This source generator will automatically create the `ExcelPropertiesToExclude.generated.cs` file.
    /// 7. When performing Excel export operations, you can utilize the generated `ExcelPropertiesToExclude.GetHeadersToExclude({YourDTO}.)` method to retrieve the list of properties to omit.
    ///
    /// **Generated Output:**
    /// - `ExcelPropertiesToExclude.generated.cs`: Contains a static class `ExcelPropertiesToExclude` with a nested static method `GetHeadersToExclude` for each of your DTO classes. Each `GetHeadersToExclude` method returns a `string[]` containing the names of the properties to exclude for that specific DTO. This array will include any enumerable properties and any properties marked with `[MapperIgnoreTarget]` in your custom `ExcelProjectTo` mapper methods.
    /// - The namespace will be `{YourBaseNamespace}.ExcelProperties`.
    ///
    /// **Dependencies:**
    /// - Assumes the existence of DTO classes in a namespace ending with `.DTO`.
    /// - Recognizes a custom mapper class (if present) typically located in a namespace ending with `.DataMappers`.
    /// - Utilizes a `[MapperIgnoreTarget]` attribute (part of the Spiderly.Shared) to mark properties for exclusion.
    /// - Depends on a helper method `IsEnumerable()` to identify enumerable properties.
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