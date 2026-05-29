using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates Angular enums (`{your-app-name}\Frontend\src\app\business\enums\enums.generated.ts`)
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
                    predicate: static (s, _) => PipelineFactory.IsEnumSyntaxTargetForGeneration(s),
                    transform: static (ctx, _) => PipelineFactory.GetEnumSemanticTargetForGeneration(ctx))
                .Where(static c => c is not null);

            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = PipelineFactory.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<ClassCategoryCodes>
                {
                    ClassCategoryCodes.Entities,
                    ClassCategoryCodes.Enums, // HACK: Because we can't make partial enums we are doing this
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = ReferencedAssemblyAnalyzer.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<ClassCategoryCodes>
                {
                    ClassCategoryCodes.Entities,
                    ClassCategoryCodes.Enums, // HACK: Because we can't make partial enums we are doing this
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();
            IncrementalValueProvider<SpiderlyConfig> config = context.GetSpiderlyConfig();

            var combined = enumDeclarations.Collect()
                .Combine(classDeclarations.Collect())
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory)
                .Combine(config);

            context.RegisterSafeImplementationSourceOutput(combined, static (spc, source) =>
            {
                var ((((enums, classDeclarations), referencedProjectClasses), callingPath), config) = source;

                Execute(enums, classDeclarations, referencedProjectClasses, callingPath, config, spc);
            });
        }

        private static void Execute(IList<EnumDeclarationSyntax> currentProjectEnums, IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (currentProjectEnums.Count == 0 && classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgEnumsGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses);

            List<SpiderlyClass> currentProjectEntities = currentProjectClasses
                .Where(x => x.HasSpiderlyEntityAttribute())
                .ToList();

            List<SpiderlyClass> currentProjectClassEnums = currentProjectClasses
                .Where(x => x.HasSpiderlyEnumAttribute())
                .ToList();

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\enums\enums.generated.ts
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "enums", "enums.generated.ts");

            string result = GetAngularEnums(currentProjectEnums, currentProjectClassEnums, currentProjectEntities);

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static string GetAngularEnums(
            IList<EnumDeclarationSyntax> currentProjectEnums,
            List<SpiderlyClass> currentProjectClassEnums,
            List<SpiderlyClass> currentProjectEntities)
        {
            // C# enums emit a get{Enum}NamebookList(translocoService) builder that returns Namebook[],
            // so the file needs those imports. Class-based enums don't (they only emit the enum itself).
            string imports = currentProjectEnums.Count > 0
                ? """
import { Namebook } from 'spiderly';
import { TranslocoService } from '@jsverse/transloco';
import { marker } from '@jsverse/transloco-keys-manager/marker';


"""
                : "";

            return $$"""
{{imports}}{{GetAngularEnumsFromCurrentProjectEnums(currentProjectEnums)}}
{{GetAngularEnumsFromCurrentProjectClassEnums(currentProjectClassEnums, currentProjectEntities)}}
""";
        }

        private static string GetAngularEnumsFromCurrentProjectEnums(IList<EnumDeclarationSyntax> currentProjectEnums)
        {
            StringBuilder sb = new();

            foreach (EnumDeclarationSyntax enume in currentProjectEnums.OrderBy(x => x.Identifier.Text).ToList())
            {
                List<SpiderlyEnumItem> enumItems = Helpers.GetEnumItems(enume);
                List<string> angularEnumItemNameValuePairs = GetAngularEnumItemNameValuePairs(enumItems);

                sb.AppendLine($$"""
export enum {{enume.Identifier.Text}}
{
    {{string.Join("\n\t", angularEnumItemNameValuePairs)}}
}

{{GetEnumNamebookListFunction(enume.Identifier.Text, enumItems)}}

""");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Emits a per-enum builder that returns the dropdown options as a <c>Namebook[]</c>, built client-side
        /// from the TS enum (no API round-trip). Each member becomes a literal
        /// <c>translocoService.translate(marker('Member'))</c> call so transloco-keys-manager's static extraction
        /// records the key; the translation key is the member name. Reused by the entity form dropdown and any
        /// list-table enum column filter.
        /// </summary>
        internal static string GetEnumNamebookListFunction(string enumName, List<SpiderlyEnumItem> enumItems)
        {
            // marker('X') is transloco-keys-manager's static-extraction hook: it returns its argument unchanged
            // (so translate() still localizes at runtime) but lets `i18n:extract` discover the key from inside this
            // standalone builder. A plain translocoService.translate('X') here is NOT extracted — the keys manager
            // only scans component/injectable classes + templates + marker() calls, not exported helper functions.
            List<string> options = enumItems
                .Select(item => $$"""        { id: {{enumName}}.{{item.Name}}, displayName: translocoService.translate(marker('{{item.Name}}')) },""")
                .ToList();

            return $$"""
export function get{{enumName}}NamebookList(translocoService: TranslocoService): Namebook[] {
    return [
{{string.Join("\n", options)}}
    ];
}
""";
        }

        private static List<string> GetAngularEnumItemNameValuePairs(List<SpiderlyEnumItem> enumItems)
        {
            List<string> result = new();

            foreach (SpiderlyEnumItem enume in enumItems)
            {
                if (enume.Value != null)
                    result.Add($"{enume.Name} = {enume.Value},");
                else
                    result.Add($"{enume.Name},");
            }

            return result;
        }

        private static string GetAngularEnumsFromCurrentProjectClassEnums(List<SpiderlyClass> currentProjectClassEnums, List<SpiderlyClass> currentProjectEntities)
        {
            StringBuilder sb = new();

            List<string> currentProjectEntitiesPermissionCodes = Helpers.GetPermissionCodesForEntites(currentProjectEntities);

            IOrderedEnumerable<IGrouping<string, SpiderlyClass>> groupedClassEnums = currentProjectClassEnums
                .GroupBy(x => x.Name)
                .OrderBy(x => x.Key);

            foreach (IGrouping<string, SpiderlyClass> group in groupedClassEnums)
            {
                List<string> propertyNames = group
                    .SelectMany(x => x.Properties.Select(p => p.Name))
                    .ToList();

                if (group.Key == "PermissionCodes")
                    propertyNames.AddRange(currentProjectEntitiesPermissionCodes);

                List<string> angularEnumItemNameValuePairs = GetAngularEnumItemNameValuePairs(propertyNames.Distinct().ToList());

                sb.AppendLine($$"""
export enum {{group.Key}}
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
