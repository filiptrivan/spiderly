using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System.Collections.Generic;
using System.Collections.Immutable;
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var ((classes, referencedClasses), config) = combinedSource;
                Execute(classes, referencedClasses, enumNames, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, ImmutableArray<string> spiderlyEnumNames, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(EntitiesToDTOGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectEntities, spiderlyEnumNames);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            if (currentProjectEntities.Count == 0)
                return;

            bool hasDisplayNameErrors = false;
            foreach (Diagnostic diagnostic in Validations.ValidateDisplayNameAttributes(currentProjectEntities, allEntities))
            {
                context.ReportDiagnostic(diagnostic);
                hasDisplayNameErrors = true;
            }
            if (hasDisplayNameErrors)
                return;

            List<SpiderlyClass> currentProjectDTOClasses = SpiderlyClassFactory.GetDTOClasses(currentProjectEntities, allEntities);

            HashSet<string> handWrittenDTONames = new HashSet<string>(
                currentProjectClasses
                    .Where(x => x.HasSpiderlyDTOAttribute())
                    .Select(x => x.Name));

            string namespaceValue = currentProjectEntities[0].Namespace;
            string basePartOfNamespace = Helpers.GetBasePartOfNamespace(namespaceValue);

            string result = $$"""
{{GetUsings(basePartOfNamespace)}}

namespace {{basePartOfNamespace}}.DTO
{
{{GetDTOClasses(currentProjectDTOClasses, handWrittenDTONames)}}
}
""";

            context.AddSource($"DTOList.generated", SourceText.From(result, Encoding.UTF8));
        }

        private static string GetDTOClasses(List<SpiderlyClass> currentProjectDTOClasses, HashSet<string> handWrittenDTONames)
        {
            List<string> result = new();

            foreach (SpiderlyClass currentProjectDTOClass in currentProjectDTOClasses)
            {
                // Skip [SpiderlyDTO] when the user wrote a hand-typed partial for this class — that partial already carries the attribute,
                // and a non-AllowMultiple marker would collide.
                string attributeLine = handWrittenDTONames.Contains(currentProjectDTOClass.Name) ? "" : "[SpiderlyDTO]\n    ";

                if (currentProjectDTOClass.Description != null)
                {
                    result.Add($$"""
    /// <summary>
    /// {{currentProjectDTOClass.Description}}
    /// </summary>
    {{attributeLine}}public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
    {
{{GetDTOProperties(currentProjectDTOClass)}}
    }
""");
                }
                else
                {
                    result.Add($$"""
    {{attributeLine}}public partial class {{currentProjectDTOClass.Name}} {{GetDTOBaseTypeExtension(currentProjectDTOClass.BaseType)}}
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

        private static string GetUsings(string basePartOfNamespace)
        {
            // {basePartOfNamespace}.Enums is needed when entity properties are typed as a
            // [SpiderlyEnum]-decorated enum — the DTO emits the enum type by short name and
            // would otherwise fail to resolve. Convention: enums always live under .Enums.
            return $$"""
using Microsoft.AspNetCore.Http;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.DTO;
using Spiderly.Security.DTO;
using Spiderly.Shared.Helpers;
using {{basePartOfNamespace}}.Enums;
""";
        }

        #endregion
    }
}
