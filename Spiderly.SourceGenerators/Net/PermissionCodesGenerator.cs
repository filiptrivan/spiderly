using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Text;
using Microsoft.CodeAnalysis.Text;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Linq;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// **Summary:**
    /// Generates a partial class `{{YourProjectName}}PermissionCodes` (`{{YourProjectName}}PermissionCodes.generated.cs`)
    /// within the `{YourBaseNamespace}.Enums` namespace. This class defines static string constants
    /// representing permission codes for each CRUD operation on your entity classes.
    ///
    /// **Key Features:**
    /// - **Automatic Permission Code Generation:** For each entity in your project (within the '.Entities' namespace),
    ///   it automatically generates static string constants for Read, Insert, Update, and Delete permissions.
    /// - **Consistent Naming Convention:** The generated permission code constants follow a consistent pattern: `{EntityName}{CrudOperation}` (e.g., `UserRead`, `UserInsert`, `RoleUpdate`, `PermissionDelete`).
    /// - **Partial Class Design:** The generated class is declared as `partial`, allowing you to extend it with additional custom permission codes if needed without modifying the generated code.
    /// - **Easy Integration:** The generated constants can be easily used within your authorization logic.
    ///
    /// **How to Use:**
    /// 1. Ensure your entity classes are located in a namespace ending with `.Entities`.
    /// 2. Build your .NET project. This source generator will automatically run during the build process.
    /// 3. The `{{YourProjectName}}PermissionCodes.generated.cs` file will be created in your `.Enums` namespace.
    /// 4. You can then reference the static string constants within this class in your authorization policies and checks.
    /// 5. If you need additional permission codes beyond the standard CRUD operations for your entities, you can create another partial class named `{{YourProjectName}}PermissionCodes` in the same namespace and define your custom constants there.
    ///
    /// **Generated Output:**
    /// - `{{YourProjectName}}PermissionCodes.generated.cs`: Contains a partial static class `{{YourProjectName}}PermissionCodes` with public static string constants for each CRUD operation on each of your entity classes. For example, if you have a `User` entity, you will find constants like `UserRead`, `UserInsert`, `UserUpdate`, and `UserDelete`.
    /// - The namespace will be `{YourBaseNamespace}.Enums`.
    ///
    /// **Dependencies:**
    /// - Assumes your entity classes are located in a namespace ending with `.Entities`.
    /// 
    /// </summary>
    [Generator]
    public class PermissionCodesGenerator : IIncrementalGenerator
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
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SourceProductionContext context)
        {
            if (classes.Count <= 1)
                return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            StringBuilder sb = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);
            string projectName = Helpers.GetProjectName(namespaceValue);

            List<string> permissionCodes = Helpers.GetPermissionCodesForEntites(currentProjectEntities);

            sb.AppendLine($$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {{basePartOfNamespace}}.Enums
{
    public partial class {{projectName}}PermissionCodes
    {
        {{string.Join("\n\t\t", permissionCodes.Select(x => $$"""public static string {{x}} { get; } = "{{x}}";"""))}}
    }
}
""");

            context.AddSource($"{projectName}PermissionCodes.generated", SourceText.From(sb.ToString(), Encoding.UTF8));
        }
    }
}

