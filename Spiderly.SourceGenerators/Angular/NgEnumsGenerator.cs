using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Diagnostics;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Enums;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates Angular enums (`{your-app-name}\Angular\src\app\business\enums\{your-app-name}-enums.generated.ts`)
    /// from C# `enum` declarations and specially marked C# classes within the '.Enums' namespace.
    /// This generator ensures type safety and consistency between your backend and frontend enum values.
    /// </summary>
    [Generator]
    public class NgEnumsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
//#if DEBUG
//            if (!Debugger.IsAttached)
//            {
//                Debugger.Launch();
//            }
//#endif
            IncrementalValuesProvider<EnumDeclarationSyntax> enumDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => Helpers.IsEnumSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => Helpers.GetEnumSemanticTargetForGeneration(ctx))
                .Where(static c => c is not null);

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.Enums, // FT HACK: Because we can't make partial enums we are doing this
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.Enums, // FT HACK: Because we can't make partial enums we are doing this
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();

            var combined = enumDeclarations.Collect()
                .Combine(classDeclarations.Collect())
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (((enums, classDeclarations), referencedProjectClasses), callingPath) = source;

                Execute(enums, classDeclarations, referencedProjectClasses, callingPath, spc);
            });
        }

        private static void Execute(IList<EnumDeclarationSyntax> currentProjectEnums, IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SourceProductionContext context)
        {
            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> currentProjectClassEnums = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Enums")).ToList();

            string namespaceValue = currentProjectEntities[0].Namespace;
            string projectName = Helpers.GetProjectName(namespaceValue);

            // ...\API\PlayertyLoyals.Business -> ...\Angular\src\app\business\enums\{projectName}-enums.ts
            string outputPath = callingProjectDirectory.ReplaceEverythingAfter(@"\API\", $@"\Angular\src\app\business\enums\{projectName.FromPascalToKebabCase()}-enums.generated.ts");

            string result = GetAngularEnums(currentProjectEnums, currentProjectClassEnums, currentProjectEntities, projectName);

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static string GetAngularEnums(
            IList<EnumDeclarationSyntax> currentProjectEnums, 
            List<SpiderlyClass> currentProjectClassEnums, 
            List<SpiderlyClass> currentProjectEntities, 
            string projectName)
        {
            return $$"""
{{GetAngularEnumsFromCurrentProjectEnums(currentProjectEnums)}}
{{GetAngularEnumsFromCurrentProjectClassEnums(currentProjectClassEnums, currentProjectEntities, projectName)}}
""";
        }

        private static string GetAngularEnumsFromCurrentProjectEnums(IList<EnumDeclarationSyntax> currentProjectEnums)
        {
            StringBuilder sb = new();

            foreach (EnumDeclarationSyntax enume in currentProjectEnums.OrderBy(x => x.Identifier.Text).ToList())
            {
                List<SpiderEnumItem> enumItems = Helpers.GetEnumItems(enume);
                List<string> angularEnumItemNameValuePairs = GetAngularEnumItemNameValuePairs(enumItems);

                sb.AppendLine($$"""
export enum {{enume.Identifier.Text}}
{
    {{string.Join("\n\t", angularEnumItemNameValuePairs)}}
}

""");
            }

            return sb.ToString();
        }

        private static List<string> GetAngularEnumItemNameValuePairs(List<SpiderEnumItem> enumItems)
        {
            List<string> result = new();

            foreach (SpiderEnumItem enume in enumItems)
            {
                if(enume.Value != null)
                    result.Add($"{enume.Name} = {enume.Value},");
                else
                    result.Add($"{enume.Name},");
            }

            return result;
        }

        private static string GetAngularEnumsFromCurrentProjectClassEnums(List<SpiderlyClass> currentProjectClassEnums, List<SpiderlyClass> currentProjectEntities, string projectName)
        {
            StringBuilder sb = new();

            List<string> currentProjectEntitiesPermissionCodes = Helpers.GetPermissionCodesForEntites(currentProjectEntities);

            foreach (SpiderlyClass classEnum in currentProjectClassEnums.OrderBy(x => x.Name).ToList())
            {
                List<string> angularEnumItemNameValuePairs = GetAngularEnumItemNameValuePairs(classEnum.Properties.Select(x => x.Name).ToList());

                if (classEnum.Name == $"{projectName}PermissionCodes")
                    angularEnumItemNameValuePairs.AddRange(GetAngularEnumItemNameValuePairs(currentProjectEntitiesPermissionCodes));

                sb.AppendLine($$"""
export enum {{classEnum.Name}}
{
    {{string.Join("\n\t", angularEnumItemNameValuePairs)}}
}

""");
            }

            return sb.ToString();
        }

        private static List<string> GetAngularEnumItemNameValuePairs(List<string> propertyNames)
        {
            return propertyNames.Select(x => $$"""{{x}} = "{{x}}",""").ToList();
        }

    }
}
