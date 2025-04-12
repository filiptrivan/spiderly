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
