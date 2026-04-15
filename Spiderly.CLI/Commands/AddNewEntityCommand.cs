using CaseConverter;
using Spectre.Console;
using Spiderly.CLI.Services;
using Spiderly.Shared.Helpers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Spiderly.CLI.Commands
{
    internal static class AddNewEntityCommand
    {
        public static async Task<int> Execute(bool shouldGenerateDataView, string entityName = null)
        {
            if (string.IsNullOrWhiteSpace(entityName))
            {
                if (!ConsoleHelper.IsInteractive())
                {
                    ConsoleHelper.MarkupLineERROR("Entity name is required in non-interactive mode. Use: spiderly add-new-entity --name YourEntityName");
                    return 1;
                }

                entityName = AnsiConsole.Prompt(
                    new TextPrompt<string>("Entity name without spaces (e.g., YourEntityName):")
                        .PromptStyle("blue")
                        .ValidationErrorMessage("[red]Entity name can't be empty or contain spaces[/]")
                        .Validate(name =>
                        {
                            if (string.IsNullOrWhiteSpace(name))
                                return ValidationResult.Error("[red]Entity name can't be null or empty[/]");

                            if (name.Contains(" "))
                                return ValidationResult.Error("[red]Entity name can't have spaces[/]");

                            if (!char.IsUpper(name[0]))
                                return ValidationResult.Error("[red]Entity name must start with an uppercase letter (PascalCase)[/]");

                            return ValidationResult.Success();
                        }));
            }

            bool hasErrors = false;

            ConsoleHelper.MarkupLineLoading("Generating files for the entity...");

            string kebabEntityName = entityName.ToKebabCase();

            await GenerateEntityFile(entityName);
            await GenerateAngularPages(entityName, kebabEntityName, shouldGenerateDataView);

            if (!await AddRoutes(entityName, kebabEntityName))
                hasErrors = true;

            if (!await AddMenuItem(entityName, kebabEntityName))
                hasErrors = true;

            if (!await AddTranslations(entityName))
                hasErrors = true;

            if (hasErrors)
            {
                ConsoleHelper.MarkupLineERROR("Command completed with errors. Some file injections failed.");
                return 1;
            }

            ConsoleHelper.MarkupLineOK("Command execution completed! Customize the generated entity to continue.");
            return 0;
        }

        private static async Task GenerateEntityFile(string entityName)
        {
            string entitiesFolderPath = GetEntitiesFolderPath();
            if (entitiesFolderPath == null)
                return;

            string appName = GetAppName(entitiesFolderPath);
            if (appName == null)
                return;

            string entityFilePath = Path.Combine(entitiesFolderPath, $"{entityName}.cs");
            if (File.Exists(entityFilePath))
            {
                ConsoleHelper.MarkupLineWARNING($"Entity file already exists: {entityName}.cs");
                return;
            }

            string entityTemplate = GetEntityTemplate(appName, entityName);
            await File.WriteAllTextAsync(entityFilePath, entityTemplate, Encoding.UTF8);
            ConsoleHelper.MarkupLineOK($"Entity file generated: [dim]{entityFilePath}[/]");
        }

        private static async Task GenerateAngularPages(string entityName, string kebabEntityName, bool shouldGenerateDataView)
        {
            string pagesFolderPath = GetPagesFolderPath();
            if (pagesFolderPath == null)
                return;

            string newPageFolderPath = Path.Combine(pagesFolderPath, kebabEntityName);
            if (Directory.Exists(newPageFolderPath))
            {
                ConsoleHelper.MarkupLineWARNING($"Page folder already exists: {kebabEntityName}");
                return;
            }

            Directory.CreateDirectory(newPageFolderPath);

            string listTsPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-list.component.ts");
            string listHtmlPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-list.component.html");
            string listTsTemplate;
            string listHtmlTemplate;

            if (shouldGenerateDataView)
            {
                listTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDataViewTsTemplate(entityName);
                listHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDataViewHtmlTemplate(entityName);
            }
            else
            {
                listTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularTableTsTemplate(entityName);
                listHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularTableHtmlTemplate(entityName);
            }

            await File.WriteAllTextAsync(listTsPath, listTsTemplate, Encoding.UTF8);
            ConsoleHelper.MarkupLineOK($"List .ts file generated: [dim]{listTsPath}[/]");

            await File.WriteAllTextAsync(listHtmlPath, listHtmlTemplate, Encoding.UTF8);
            ConsoleHelper.MarkupLineOK($"List .html file generated: [dim]{listHtmlPath}[/]");

            string detailsTsPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.ts");
            string detailsTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsTsTemplate(entityName);
            await File.WriteAllTextAsync(detailsTsPath, detailsTsTemplate, Encoding.UTF8);
            ConsoleHelper.MarkupLineOK($"Details .ts file generated: [dim]{detailsTsPath}[/]");

            string detailsHtmlPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.html");
            string detailsHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsHtmlTemplate(entityName);
            await File.WriteAllTextAsync(detailsHtmlPath, detailsHtmlTemplate, Encoding.UTF8);
            ConsoleHelper.MarkupLineOK($"Details .html file generated: [dim]{detailsHtmlPath}[/]");
        }

        private static async Task<bool> AddRoutes(string entityName, string kebabEntityName)
        {
            string routesFilePath = GetAppRoutesFilePath();
            if (routesFilePath == null)
                return ConsoleHelper.IsInteractive(); // Interactive: user can fix manually (success). Non-interactive: unrecoverable (failure).

            string routesContent = await File.ReadAllTextAsync(routesFilePath, Encoding.UTF8);

            string listRoutePath = $"'{kebabEntityName}-list'";
            if (routesContent.Contains(listRoutePath))
            {
                ConsoleHelper.MarkupLineWARNING($"Routes already exist for: {kebabEntityName}-list");
                return true;
            }

            string routesToAdd = $$"""
                {
                    path: '{{kebabEntityName}}-list',
                    loadComponent: () => import('./pages/{{kebabEntityName}}/{{kebabEntityName}}-list.component').then(c => c.{{entityName}}ListComponent),
                    canActivate: [AuthGuard],
                },
                {
                    path: '{{kebabEntityName}}-list/:id',
                    loadComponent: () => import('./pages/{{kebabEntityName}}/{{kebabEntityName}}-details.component').then(c => c.{{entityName}}DetailsComponent),
                    canActivate: [AuthGuard],
                },
            """;

            string pattern = @"(const layoutRoutes: Routes = \[\s*\{[^}]+\},?)";
            Match match = Regex.Match(routesContent, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                int insertPosition = match.Index + match.Length;
                string newContent = routesContent.Insert(insertPosition, "\n" + routesToAdd);
                await File.WriteAllTextAsync(routesFilePath, newContent, Encoding.UTF8);
                ConsoleHelper.MarkupLineOK($"Routes added for: {kebabEntityName}-list");
                return true;
            }

            if (!ConsoleHelper.IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("Could not find the appropriate location to insert routes. Route injection failed.");
                return false;
            }

            ConsoleHelper.MarkupLineWARNING("Could not find the appropriate location to insert routes. Please add them manually.");
            return true;
        }

        private static async Task<bool> AddMenuItem(string entityName, string kebabEntityName)
        {
            string layoutFilePath = GetLayoutComponentFilePath();
            if (layoutFilePath == null)
                return ConsoleHelper.IsInteractive(); // Interactive: user can fix manually (success). Non-interactive: unrecoverable (failure).

            string layoutContent = await File.ReadAllTextAsync(layoutFilePath, Encoding.UTF8);

            string routerLinkPattern = $"routerLink: \\[.*{kebabEntityName}-list.*\\]";
            if (Regex.IsMatch(layoutContent, routerLinkPattern))
            {
                ConsoleHelper.MarkupLineWARNING($"Menu item already exists for: {kebabEntityName}-list");
                return true;
            }

            string menuItemToAdd = $$"""
                                {
                                    label: this.translocoService.translate('{{entityName}}List'),
                                    icon: 'pi pi-fw pi-list',
                                    routerLink: ['/{{kebabEntityName}}-list'],
                                },
            """;

            string pattern = @"(this\.menu = \[\s*\{\s*items: \[)";
            Match match = Regex.Match(layoutContent, pattern, RegexOptions.Singleline);

            if (match.Success)
            {
                string homeItemPattern = @"(\{\s*label:\s*this\.translocoService\.translate\('Home'\)[^}]+\},?)";
                Match homeMatch = Regex.Match(layoutContent, homeItemPattern, RegexOptions.Singleline);

                if (homeMatch.Success)
                {
                    int insertPosition = homeMatch.Index + homeMatch.Length;
                    string newContent = layoutContent.Insert(insertPosition, "\n" + menuItemToAdd);
                    await File.WriteAllTextAsync(layoutFilePath, newContent, Encoding.UTF8);
                    ConsoleHelper.MarkupLineOK($"Menu item added for: {entityName}List");
                }
                else
                {
                    int insertPosition = match.Index + match.Length;
                    string newContent = layoutContent.Insert(insertPosition, "\n" + menuItemToAdd);
                    await File.WriteAllTextAsync(layoutFilePath, newContent, Encoding.UTF8);
                    ConsoleHelper.MarkupLineOK($"Menu item added for: {entityName}List");
                }

                return true;
            }

            if (!ConsoleHelper.IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("Could not find the appropriate location to insert menu item. Menu injection failed.");
                return false;
            }

            ConsoleHelper.MarkupLineWARNING("Could not find the appropriate location to insert menu item. Please add it manually.");
            return true;
        }

        private static async Task<bool> AddTranslations(string entityName)
        {
            string splitName = Regex.Replace(entityName, @"(\B[A-Z])", " $1");

            Dictionary<string, string> keysToAdd = new()
            {
                { entityName, splitName },
                { $"{entityName}List", $"{splitName} List" },
            };

            bool angularOk = await AddTranslationKeysToFolder(GetAngularI18nFolderPath(), keysToAdd, "Angular i18n");
            bool backendOk = await AddTranslationKeysToFolder(GetBackendTranslationsFolderPath(), keysToAdd, "Backend translations");

            return angularOk && backendOk;
        }

        private static async Task<bool> AddTranslationKeysToFolder(string folderPath, Dictionary<string, string> keysToAdd, string label)
        {
            if (folderPath == null)
                return false;

            string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");
            JsonSerializerOptions options = new()
            {
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            };

            foreach (string jsonFile in jsonFiles)
            {
                string json = await File.ReadAllTextAsync(jsonFile, Encoding.UTF8);
                JsonObject root = JsonNode.Parse(json)?.AsObject();
                if (root == null)
                    continue;

                bool changed = false;
                foreach (KeyValuePair<string, string> kvp in keysToAdd)
                {
                    JsonNode existing = root[kvp.Key];
                    if (existing == null || (existing is JsonValue val && string.IsNullOrEmpty(val.ToString())))
                    {
                        root[kvp.Key] = kvp.Value;
                        changed = true;
                    }
                }

                if (changed)
                {
                    string updatedJson = root.ToJsonString(options);
                    await File.WriteAllTextAsync(jsonFile, updatedJson, Encoding.UTF8);
                }
            }

            ConsoleHelper.MarkupLineOK($"{label} translation keys added");
            return true;
        }

        private static string GetAngularI18nFolderPath()
        {
            string currentPath = Directory.GetCurrentDirectory();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(currentPath, "Frontend", "src", "assets", "i18n"),
                Path.Combine(currentPath, "..", "Frontend", "src", "assets", "i18n"),
                Path.Combine(currentPath, "src", "assets", "i18n"),
            }
            .Select(Path.GetFullPath)
            .ToList();

            string existingPath = candidatePaths.FirstOrDefault(Directory.Exists);
            if (existingPath != null)
                return existingPath;

            ConsoleHelper.MarkupLineWARNING("Could not find Angular i18n folder for translation injection.");
            return null;
        }

        private static string GetBackendTranslationsFolderPath()
        {
            string backendPath = FindBackendPath();
            if (backendPath != null)
            {
                string sharedFolder = Directory.GetDirectories(backendPath, "*.Shared").FirstOrDefault();
                if (sharedFolder != null)
                {
                    string translationsPath = Path.Combine(sharedFolder, "Translations");
                    if (Directory.Exists(translationsPath))
                        return translationsPath;
                }
            }

            ConsoleHelper.MarkupLineWARNING("Could not find Backend Translations folder for translation injection.");
            return null;
        }

        private static string GetEntityTemplate(string appName, string entityName)
        {
            return $$"""
using Spiderly.Shared.BaseEntities;
using Spiderly.Shared.Attributes.Entity;
using Spiderly.Shared.Attributes.Entity.UI;
using System.ComponentModel.DataAnnotations;

namespace {{appName}}.Business.Entities
{
    [SpiderlyEntity]
    [DoNotAuthorize]
    public class {{entityName}} : BusinessObject<long>
    {

    }
}
""";
        }

        private static string GetEntitiesFolderPath()
        {
            string backendPath = FindBackendPath();
            if (backendPath != null)
            {
                string businessFolder = Directory.GetDirectories(backendPath, "*.Business").FirstOrDefault();
                if (businessFolder != null)
                {
                    string entitiesPath = Path.Combine(businessFolder, "Entities");
                    if (Directory.Exists(entitiesPath))
                        return entitiesPath;
                }
            }

            ConsoleHelper.MarkupLineERROR("Could not find Entities folder. Please run this command from within your Spiderly project directory.");
            return null;
        }

        private static string GetAppName(string entitiesFolderPath)
        {
            string businessFolder = Directory.GetParent(entitiesFolderPath)?.FullName;
            if (businessFolder != null)
            {
                string folderName = Path.GetFileName(businessFolder);
                if (folderName.EndsWith(".Business"))
                {
                    return folderName.Replace(".Business", "");
                }
            }

            ConsoleHelper.MarkupLineERROR("Could not determine app name from project structure.");
            return null;
        }

        private static string GetPagesFolderPath()
        {
            string currentPath = Directory.GetCurrentDirectory();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(currentPath, "src", "app", "pages"),
                Path.Combine(currentPath, "..", "Frontend", "src", "app", "pages"),
                Path.Combine(currentPath, "Frontend", "src", "app", "pages"),
                Path.Combine(currentPath, "Frontend", "src", "app", "features"),
            }
            .Select(Path.GetFullPath)
            .ToList();

            string existingPath = candidatePaths.FirstOrDefault(Directory.Exists);
            if (existingPath != null)
                return existingPath;

            ConsoleHelper.MarkupLineERROR("Expected frontend project structure was not detected.");
            AnsiConsole.MarkupLine("Tried the following paths:");
            foreach (string path in candidatePaths)
            {
                AnsiConsole.MarkupLine($"  [dim]{path}[/]");
            }

            return null;
        }

        private static string GetAppRoutesFilePath()
        {
            string currentPath = Directory.GetCurrentDirectory();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(currentPath, "src", "app", "app.routes.ts"),
                Path.Combine(currentPath, "..", "Frontend", "src", "app", "app.routes.ts"),
                Path.Combine(currentPath, "Frontend", "src", "app", "app.routes.ts"),
            }
            .Select(Path.GetFullPath)
            .ToList();

            string existingPath = candidatePaths.FirstOrDefault(File.Exists);
            if (existingPath != null)
                return existingPath;

            ConsoleHelper.MarkupLineERROR("Could not find app.routes.ts file.");
            return null;
        }

        private static string GetLayoutComponentFilePath()
        {
            string currentPath = Directory.GetCurrentDirectory();

            List<string> candidatePaths = new List<string>
            {
                Path.Combine(currentPath, "src", "app", "business", "layout", "layout.component.ts"),
                Path.Combine(currentPath, "..", "Frontend", "src", "app", "business", "layout", "layout.component.ts"),
                Path.Combine(currentPath, "Frontend", "src", "app", "business", "layout", "layout.component.ts"),
            }
            .Select(Path.GetFullPath)
            .ToList();

            string existingPath = candidatePaths.FirstOrDefault(File.Exists);
            if (existingPath != null)
                return existingPath;

            ConsoleHelper.MarkupLineERROR("Could not find layout.component.ts file.");
            return null;
        }

        private static string FindBackendPath()
        {
            string currentDir = Environment.CurrentDirectory;

            if (Path.GetFileName(currentDir) == "Backend")
                return currentDir;

            string backendInCurrent = Path.Combine(currentDir, "Backend");
            if (Directory.Exists(backendInCurrent))
                return backendInCurrent;

            string parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null && Path.GetFileName(parentDir) == "Backend")
                return parentDir;

            return null;
        }
    }
}
