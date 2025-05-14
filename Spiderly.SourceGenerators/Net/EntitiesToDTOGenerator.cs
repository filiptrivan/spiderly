using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using System.Linq;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Enums;
using System.Diagnostics;
using System;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// **Summary:**
    /// Generates partial DTO (Data Transfer Object) classes (`{YourProjectName}DTOList.generated.cs`)
    /// within the `{YourBaseNamespace}.DTO` namespace. These DTOs are automatically created
    /// based on your entity classes located in the '.Entities' namespace, providing a
    /// separate representation of your data for transfer purposes.
    ///
    /// **Key Features:**
    /// - **Automatic DTO Generation:** Creates a partial DTO class for each entity class found.
    /// - **Property Mapping:** Includes properties in the DTO that directly correspond to the properties of the respective entity. Base class properties are excluded to leverage inheritance.
    /// - **Extensibility:** Generates partial classes, allowing you to add custom properties, methods, and attributes to your DTOs without modifying the generated code.
    /// - **Namespace Alignment:** Places the generated DTO classes in a `.DTO` sub-namespace, mirroring the `.Entities` namespace structure.
    /// - **Base Type Support:** Respects inheritance hierarchies by extending the DTO from the base type of the corresponding entity, if one exists.
    ///
    /// **How to Use:**
    /// 1. Ensure your entity classes are located in a namespace ending with `.Entities`.
    /// 2. Build your .NET project. This source generator will automatically run during the build process.
    /// 3. The generated DTO classes will be created in a `.DTO` sub-namespace.
    /// 4. You can then use these generated DTOs in your services, controllers, and other parts of your application for data transfer.
    /// 5. To add custom properties or logic to a DTO, create another partial class with the same name in the same namespace.
    ///
    /// **Generated Output:**
    /// - `{YourProjectName}DTOList.generated.cs`: Contains partial classes, one for each entity.
    ///     - Each partial class will have properties matching the non-inherited properties of its corresponding entity.
    ///     - If the entity inherits from another entity, the DTO will inherit from the DTO of the base entity.
    /// - The namespace will be `{YourBaseNamespace}.DTO`.
    ///
    /// **Note:** This generator focuses on creating a basic DTO structure mirroring your entities. You might need to manually add more complex properties or properties that you want in DTO but not in Entity.
    /// </summary>
    [Generator]
    public class EntitiesToDTOGenerator : IIncrementalGenerator
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
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                });

            var allClasses = classDeclarations.Collect()
                .Combine(referencedProjectClasses);

            context.RegisterImplementationSourceOutput(allClasses, static (spc, source) => Execute(source.Left, source.Right, spc));

            //context.RegisterImplementationSourceOutput(classDeclarations.Collect(),
            //    static (spc, source) => Execute(source, spc));
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SourceProductionContext context)
        {
            if (classes.Count <= 1)
                return;

            List<SpiderlyClass> currentProjectEntities = Helpers.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();
            List<SpiderlyClass> currentProjectDTOClasses = Helpers.GetDTOClasses(currentProjectEntities, allEntities);

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            string result = $$"""
{{GetUsings()}}

namespace {{basePartOfNamespace}}.DTO
{
{{GetDTOClasses(currentProjectDTOClasses, currentProjectEntities, allEntities)}}
}
""";

            context.AddSource($"{projectName}DTOList.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static string GetDTOClasses(List<SpiderlyClass> currentProjectDTOClasses, List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyClass currentProjectDTOClass in currentProjectDTOClasses)
            {
                result.Add($$"""
    public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass)}}
    }
""");
            }

            return string.Join("\n\n", result);
        }

        /// <summary>
        /// Getting the properties of the DTO based on the entity class, we don't include base type properties because of the inheritance
        /// </summary>
        private static string GetDTOProperties(SpiderlyClass currentProjectDTOClass)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in currentProjectDTOClass.Properties)
            {
                if (property.EntityName != currentProjectDTOClass.Name)
                    continue;

                result.Add($$"""
        public {{property.Type}} {{property.Name}} { get; set; }
""");
            }

            return string.Join("\n", result);
        }

        #region Helpers

        private static string GetDTOBaseTypeExtension(string DTObaseType)
        {
            return DTObaseType == null ? "" : $": {DTObaseType}";
        }

        private static string GetUsings()
        {
            return $$"""
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.DTO;
using Spiderly.Security.DTO;
using Spiderly.Shared.Helpers;
""";
        }

        #endregion
    }
}
