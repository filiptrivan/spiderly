using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Generates translation resource files (`TermsGenerated.resx`, `TermsGenerated.sr-Latn-RS.resx`)
    /// in the `.Shared` project and Angular i18n JSON files (`en.generated.json`, `sr-Latn-RS.generated.json`)
    /// in the Angular project's `src/assets/i18n` folder. These files contain translations for
    /// entity names (singular and plural), Excel export names, and property names, based on
    /// the `[Translate]` attribute applied to your entity classes and their properties.
    /// </summary>
    [Generator]
    public class TranslationsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = context.SyntaxProvider
                .CreateSyntaxProvider(
                    predicate: static (s, _) => Helpers.IsSyntaxTargetForGenerationEveryClass(s),
                    transform: static (ctx, _) => Helpers.GetSemanticTargetForGenerationEveryClass(ctx))
                .Where(static c => c is not null);

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();

            var combined = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntities, callingPath) = source;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, string callingProjectDirectory, SourceProductionContext context)
        {
            if (classes.Count < 1)
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            referencedProjectEntities = referencedProjectEntities.OrderBy(x => x.Name).ToList();

            Dictionary<string, string> dataEn = referencedProjectEntities
                .Select(entity => GetTranslationData(entity, LanguageCodes.En))
                .PrepareForTranslation();

            Dictionary<string, string> dataSrLatnRS = referencedProjectEntities
                .Select(entity => GetTranslationData(entity, LanguageCodes.SrLatnRS))
                .PrepareForTranslation();

            string sharedBusinessProjectPath = callingProjectDirectory.Replace(".WebAPI", ".Shared");

            Helpers.WriteResourceFile(dataEn, Path.Combine(sharedBusinessProjectPath, GetTermsFilePath(LanguageCodes.En)));
            Helpers.WriteResourceFile(dataSrLatnRS, Path.Combine(sharedBusinessProjectPath, GetTermsFilePath(LanguageCodes.SrLatnRS)));

            // your-app-name\Backend\YourAppName.Shared -> your-app-name\Frontend\src\assets\i18n
            string angulari18nFolderPath = callingProjectDirectory.ReplaceEverythingAfter(@"\Backend\", @"\Frontend\src\assets\i18n");

            Helpers.WriteToTheFile(GetJsonFromDictionaryList(dataEn), Path.Combine(angulari18nFolderPath, GetAngulari18nFilePath(LanguageCodes.En)));
            Helpers.WriteToTheFile(GetJsonFromDictionaryList(dataSrLatnRS), Path.Combine(angulari18nFolderPath, GetAngulari18nFilePath(LanguageCodes.SrLatnRS)));
        }

        private static Dictionary<string, string> GetTranslationData(SpiderlyClass entity, LanguageCodes language)
        {
            Dictionary<string, string> dictionary = new()
            {
                { $"{entity.Name}", entity.Translate(language) },
                { $"{entity.Name}List", entity.Translate(language, TranslationCodes.Plural) },
                { $"{entity.Name}ExcelExportName", entity.Translate(language, TranslationCodes.Excel) ?? entity.Translate(language, TranslationCodes.Plural).ToTrainCase() },
            };

            foreach (SpiderlyProperty property in entity.Properties)
                dictionary.Add(property.Name, property.Translate(language));

            return dictionary;
        }

        private static string GetTermsFilePath(LanguageCodes language)
        {
            return @$"Resources\TermsGenerated{GetTermsFileExtension(language)}.resx"; ;
        }

        private static string GetTermsFileExtension(LanguageCodes language)
        {
            string extension = null;

            if (language == LanguageCodes.En)
            {
                extension = null;
            }
            else if (language == LanguageCodes.SrLatnRS)
            {
                extension = ".sr-Latn-RS";
            }

            return extension;
        }

        private static string GetJsonFromDictionaryList(Dictionary<string, string> data)
        {
            return $$"""
{
{{string.Join(",\n", GetJsonElementsFromDictionaryList(data))}}
}
""";
        }

        private static List<string> GetJsonElementsFromDictionaryList(Dictionary<string, string> data)
        {
            List<string> result = new();
            Dictionary<string, string> alreadyAddedKeyValuePairs = new Dictionary<string, string>();

            foreach (KeyValuePair<string, string> keyValuePair in data)
            {
                KeyValuePair<string, string> alreadyAddedKeyValuePair = alreadyAddedKeyValuePairs.Where(x => x.Key == keyValuePair.Key).SingleOrDefault();

                if (alreadyAddedKeyValuePair.Key == null)
                {
                    alreadyAddedKeyValuePairs.Add(keyValuePair.Key, keyValuePair.Value);

                    string row = $$"""
    "{{keyValuePair.Key}}": "{{keyValuePair.Value}}"
""";

                    result.Add(row);
                }
            }

            return result;
        }

        private static string GetAngulari18nFilePath(LanguageCodes language)
        {
            string filePath = null;

            if (language == LanguageCodes.En)
            {
                filePath = "en.generated.json";
            }
            else if (language == LanguageCodes.SrLatnRS)
            {
                filePath = "sr-Latn-RS.generated.json";
            }

            return filePath;
        }

    }
}
