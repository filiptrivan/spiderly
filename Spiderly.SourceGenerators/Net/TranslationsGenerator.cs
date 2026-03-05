using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Models;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Spiderly.SourceGenerators.Net
{
    /// <summary>
    /// Reads translation JSON files from `{Shared}/Translations/` and generates:
    /// 1. `TermsGenerated.generated.cs` — C# class with inline Dictionary for all translations
    /// 2. `{lang}.generated.json` — Angular i18n JSON files in `Frontend/src/assets/i18n/`
    ///
    /// Auto-scaffolds missing keys (entity names, plural forms, Excel export names, property names)
    /// into the source JSON files with empty string values.
    /// </summary>
    [Generator]
    public class TranslationsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var combined = PipelineFactory.CreatePipelineWithCallingPath(context,
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities });

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntitiesAndPath, config) = source;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectEntities, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (referencedProjectEntities.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(TranslationsGenerator)))
                return;

            if (callingProjectDirectory.Contains(".WebAPI") == false)
                return;

            referencedProjectEntities = referencedProjectEntities.OrderBy(x => x.Name).ToList();

            string sharedProjectPath = callingProjectDirectory.Replace(".WebAPI", ".Shared");
            string translationsFolder = Path.Combine(sharedProjectPath, "Translations");

            if (!Directory.Exists(translationsFolder))
                return;

            string[] jsonFiles = Directory.GetFiles(translationsFolder, "*.json");

            if (jsonFiles.Length == 0)
                return;

            List<string> expectedKeys = BuildExpectedKeys(referencedProjectEntities);

            Dictionary<string, Dictionary<string, string>> allLanguageTranslations = new Dictionary<string, Dictionary<string, string>>();

            foreach (string jsonFile in jsonFiles)
            {
                string langCode = Path.GetFileNameWithoutExtension(jsonFile);
                Dictionary<string, string> translations = ReadJsonTranslations(jsonFile);

                ScaffoldMissingKeys(translations, expectedKeys);
                Dictionary<string, string> sorted = SortDictionary(translations);

                WriteJsonTranslations(jsonFile, sorted);

                allLanguageTranslations[langCode] = sorted;
            }

            string generatedCs = GenerateTermsGeneratedCs(allLanguageTranslations, sharedProjectPath);
            Helpers.WriteToTheFile(generatedCs, Path.Combine(sharedProjectPath, "Resources", "TermsGenerated.generated.cs"));

            string rootPath = callingProjectDirectory.GetRootPath();
            string angulari18nFolderPath = Path.Combine(rootPath, "Frontend", "src", "assets", "i18n");

            foreach (KeyValuePair<string, Dictionary<string, string>> langEntry in allLanguageTranslations)
            {
                string angularJson = GenerateAngularJson(langEntry.Value);
                Helpers.WriteToTheFile(angularJson, Path.Combine(angulari18nFolderPath, $"{langEntry.Key}.generated.json"));
            }
        }

        private static List<string> BuildExpectedKeys(List<SpiderlyClass> entities)
        {
            HashSet<string> keys = new HashSet<string>();

            foreach (SpiderlyClass entity in entities)
            {
                keys.Add(entity.Name);
                keys.Add($"{entity.Name}List");
                keys.Add($"{entity.Name}ExcelExportName");

                foreach (SpiderlyProperty property in entity.Properties)
                {
                    keys.Add(property.Name);
                }
            }

            return keys.OrderBy(x => x).ToList();
        }

        private static Dictionary<string, string> ReadJsonTranslations(string filePath)
        {
            string json = File.ReadAllText(filePath);

            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, string>();

            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }

        private static void ScaffoldMissingKeys(Dictionary<string, string> translations, List<string> expectedKeys)
        {
            foreach (string key in expectedKeys)
            {
                if (!translations.ContainsKey(key))
                {
                    translations[key] = "";
                }
            }
        }

        private static Dictionary<string, string> SortDictionary(Dictionary<string, string> dictionary)
        {
            return dictionary.OrderBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);
        }

        private static void WriteJsonTranslations(string filePath, Dictionary<string, string> translations)
        {
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            string json = JsonSerializer.Serialize(translations, options);
            json = json.Replace("\r\n", "\n");

            File.WriteAllText(filePath, json, Encoding.UTF8);
        }

        private static string GenerateTermsGeneratedCs(Dictionary<string, Dictionary<string, string>> allLanguageTranslations, string sharedProjectPath)
        {
            string namespaceName = new DirectoryInfo(sharedProjectPath).Name + ".Resources";

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Globalization;");
            sb.AppendLine("using Spiderly.Shared.Extensions;");
            sb.AppendLine();
            sb.AppendLine($"namespace {namespaceName}");
            sb.AppendLine("{");
            sb.AppendLine("    public static class TermsGenerated");
            sb.AppendLine("    {");
            sb.AppendLine("        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new Dictionary<string, Dictionary<string, string>>()");
            sb.AppendLine("        {");

            foreach (KeyValuePair<string, Dictionary<string, string>> langEntry in allLanguageTranslations)
            {
                sb.AppendLine($"            {{ \"{langEntry.Key}\", new Dictionary<string, string>()");
                sb.AppendLine("                {");

                foreach (KeyValuePair<string, string> kvp in langEntry.Value)
                {
                    string escapedValue = kvp.Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
                    sb.AppendLine($"                    {{ \"{kvp.Key}\", \"{escapedValue}\" }},");
                }

                sb.AppendLine("                }");
                sb.AppendLine("            },");
            }

            sb.AppendLine("        };");
            sb.AppendLine();
            sb.AppendLine("        public static string GetTranslation(string key)");
            sb.AppendLine("        {");
            sb.AppendLine("            string langCode = CultureInfo.CurrentCulture.Name;");
            sb.AppendLine();
            sb.AppendLine("            if (_translations.TryGetValue(langCode, out Dictionary<string, string> langDict) && langDict.TryGetValue(key, out string value) && !string.IsNullOrEmpty(value))");
            sb.AppendLine("                return value;");
            sb.AppendLine();
            sb.AppendLine("            return null;");
            sb.AppendLine("        }");
            sb.AppendLine();
            sb.AppendLine("        public static string GetExcelTranslation(string excelKey, string pluralKey)");
            sb.AppendLine("        {");
            sb.AppendLine("            string result = GetTranslation(excelKey);");
            sb.AppendLine();
            sb.AppendLine("            if (result == null)");
            sb.AppendLine("                result = GetTranslation(pluralKey);");
            sb.AppendLine();
            sb.AppendLine("            if (result == null)");
            sb.AppendLine("                result = Spiderly.Shared.Resources.SharedTerms.ResourceManager.GetTranslation(pluralKey);");
            sb.AppendLine();
            sb.AppendLine("            return string.IsNullOrEmpty(result) ? pluralKey : result;");
            sb.AppendLine("        }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            return sb.ToString();
        }

        private static string GenerateAngularJson(Dictionary<string, string> translations)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("{");

            List<KeyValuePair<string, string>> entries = translations.ToList();

            for (int i = 0; i < entries.Count; i++)
            {
                string escapedValue = entries[i].Value.Replace("\\", "\\\\").Replace("\"", "\\\"");
                string comma = i < entries.Count - 1 ? "," : "";
                sb.AppendLine($"    \"{entries[i].Key}\": \"{escapedValue}\"{comma}");
            }

            sb.AppendLine("}");

            return sb.ToString();
        }
    }
}
