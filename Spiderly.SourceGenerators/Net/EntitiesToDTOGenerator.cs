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
    /// Generates partial DTO (Data Transfer Object) classes (`{YourAppName}DTOList.generated.cs`)
    /// within the `{YourBaseNamespace}.DTO` namespace. These DTOs are automatically created
    /// based on your entity classes located in the '.Entities' namespace, providing a
    /// separate representation of your data for transfer purposes.
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
            var combined = PipelineFactory.CreatePipeline(context,
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities });

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var ((classes, referencedClasses), config) = source;
                Execute(classes, referencedClasses, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(EntitiesToDTOGenerator)))
                return;

            List<SpiderlyClass> currentProjectEntities = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities);
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            bool hasDisplayNameErrors = false;
            foreach (Diagnostic diagnostic in Validations.ValidateDisplayNameAttributes(currentProjectEntities, allEntities))
            {
                context.ReportDiagnostic(diagnostic);
                hasDisplayNameErrors = true;
            }
            if (hasDisplayNameErrors)
                return;

            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectEntities, allEntities);

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            string result = $$"""
{{GetUsings()}}

namespace {{basePartOfNamespace}}.DTO
{
{{GetDTOClasses(currentProjectDTOClasses, currentProjectEntities, allEntities)}}
}
""";

            context.AddSource($"DTOList.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static string GetDTOClasses(List<SpiderlyClass> currentProjectDTOClasses, List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyClass currentProjectDTOClass in currentProjectDTOClasses)
            {
                if (currentProjectDTOClass.Description != null)
                {
                    result.Add($$"""
    /// <summary>
    /// {{currentProjectDTOClass.Description}}
    /// </summary>
    public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass)}}
    }
""");
                }
                else
                {
                    result.Add($$"""
    public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass)}}
    }
""");
                }
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

                string listInitializer = property.Type.IsEnumerable() ? " = new();" : "";

                if (property.Description != null)
                {
                    result.Add($$"""
        /// <summary>
        /// {{property.Description}}
        /// </summary>
        public {{property.Type}} {{property.Name}} { get; set; }{{listInitializer}}
""");
                }
                else
                {
                    result.Add($$"""
        public {{property.Type}} {{property.Name}} { get; set; }{{listInitializer}}
""");
                }
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
