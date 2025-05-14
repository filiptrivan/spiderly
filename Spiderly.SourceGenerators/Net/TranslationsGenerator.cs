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
    /// **Summary:**
    /// Generates translation resource files (`TermsGenerated.resx`, `TermsGenerated.sr-Latn-RS.resx`)
    /// in the `.Shared` project and Angular i18n JSON files (`en.generated.json`, `sr-Latn-RS.generated.json`)
    /// in the Angular project's `src/assets/i18n` folder. These files contain translations for
    /// entity names (singular and plural), Excel export names, and property names, based on
    /// the `[Translate]` attribute applied to your entity classes and their properties.
    ///
    /// **Key Features:**
    /// - **Automatic Translation Key Generation:** For each entity and its properties, it generates translation keys based on their names.
    /// - **Attribute-Driven Translation:** Uses the `[Translate]` attribute on entities and properties to determine their translated values for different languages.
    /// - **Support for Singular, Plural, and Excel Forms:** Generates translations specifically for singular and plural entity names, as well as names suitable for Excel export.
    /// - **Language-Specific Files:** Creates separate resource and JSON files for each supported language (currently English and Serbian Latin).
    /// - **Angular i18n Compatibility:** Generates JSON files in a format directly usable by Angular's internationalization (i18n) system.
    /// - **Consistent File Paths:** Places the generated resource files in the `.Shared` project's `Resources` folder and the Angular i18n files in the Angular project's `src/assets/i18n` folder.
    /// - **Handles Duplicate Keys:** Prevents duplicate keys in the generated files.
    ///
    /// **How to Use:**
    /// 1. Decorate your entity classes and their properties with the `[Translate]` attribute to provide translations for different languages. The attribute takes the `TranslationCodes` (Singular, Plural, Excel) and the translated string as arguments. For properties, only the translated string is needed.
    /// 2. Ensure your `.WebAPI` project is in the same solution as your `.Shared` and Angular projects, following a standard project structure.
    /// 3. Build your .NET solution. This source generator will automatically run during the build process of your `.WebAPI` project.
    /// 4. The generated resource files (`TermsGenerated.resx`, `TermsGenerated.sr-Latn-RS.resx`) will be created in the `Resources` folder of your `.Shared` project.
    /// 5. The generated Angular i18n files (`en.generated.json`, `sr-Latn-RS.generated.json`) will be created in the `src/assets/i18n` folder of your Angular project.
    /// 6. In your .NET code, you can use the generated resource files (typically accessed via a `TermsGenerated` class) to retrieve translations.
    /// 7. In your Angular application, configure Angular `transloco` to use the generated JSON files for providing translations to your users.
    ///
    /// **Generated Output:**
    /// - `.Shared/Resources/TermsGenerated.resx`: Contains English translations as key-value pairs.
    /// - `.Shared/Resources/TermsGenerated.sr-Latn-RS.resx`: Contains Serbian Latin translations as key-value pairs.
    /// - `Angular/src/assets/i18n/en.generated.json`: Contains English translations in JSON format.
    /// - `Angular/src/assets/i18n/sr-Latn-RS.generated.json`: Contains Serbian Latin translations in JSON format.
    /// - The keys in these files will correspond to your entity names (with suffixes like "List", "ExcelExportName") and your property names.
    /// - The values will be the translated strings you provided via the `[Translate]` attribute.
    ///
    /// **Dependencies:**
    /// - Requires your `.WebAPI`, `.Shared`, and Angular projects to be in the same solution with a recognizable folder structure.
    /// - `Spiderly.Shared`
    /// 
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

            // E:\Projects\PlayertyLoyals\API\PlayertyLoyals.Shared -> E:\Projects\PlayertyLoyals\Angular\src\assets\i18n
            string angulari18nFolderPath = callingProjectDirectory.ReplaceEverythingAfter(@"\API\", @"\Angular\src\assets\i18n");

            Helpers.WriteToTheFile(GetJsonFromDictionaryList(dataEn), Path.Combine(angulari18nFolderPath, GetAngulari18nFilePath(LanguageCodes.En)));
            Helpers.WriteToTheFile(GetJsonFromDictionaryList(dataSrLatnRS), Path.Combine(angulari18nFolderPath, GetAngulari18nFilePath(LanguageCodes.SrLatnRS)));
        }

        private static Dictionary<string, string> GetTranslationData(SpiderlyClass entity, LanguageCodes language)
        {
            Dictionary<string, string> dictionary = new()
            {
                { $"{entity.Name}", entity.Translate(TranslationCodes.Singular, language) },
                { $"{entity.Name}List", entity.Translate(TranslationCodes.Plural, language) },
                { $"{entity.Name}ExcelExportName", entity.Translate(TranslationCodes.Excel, language) ?? entity.Translate(TranslationCodes.Plural, language).ToTrainCase() },
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
