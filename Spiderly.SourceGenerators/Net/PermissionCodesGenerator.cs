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
            var combined = PipelineFactory.CreatePipeline(context,
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (((classes, referencedClasses), config), nullableContext) = source;
                Execute(classes, referencedClasses, config, nullableContext, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SpiderlyConfig config, NullableContextOptions nullableContext, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(PermissionCodesGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            StringBuilder sb = new();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            List<string> permissionCodes = Helpers.GetPermissionCodesForEntites(currentProjectEntities);

            sb.AppendLine($$"""
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace {{basePartOfNamespace}}.Enums
{
    public partial class PermissionCodes
    {
        {{string.Join("\n\t\t", permissionCodes.Select(x => $$"""public static string {{x}} { get; } = "{{x}}";"""))}}
    }
}
""");

            context.AddSpiderlyCSharpSource("PermissionCodes.generated", sb.ToString(), nullableContext);
        }
    }
}

