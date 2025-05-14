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
    /// Generates a partial class `{{YourAppName}}PermissionCodes` (`{{YourAppName}}PermissionCodes.generated.cs`)
    /// within the `{YourBaseNamespace}.Enums` namespace. This class defines static string constants
    /// representing permission codes for each CRUD operation on your entity classes.
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

