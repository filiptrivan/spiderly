using CaseConverter;
using Spectre.Console;
using Spiderly.CLI.Services;
using Spiderly.Shared.Helpers;
using System.Text;

namespace Spiderly.CLI.Commands
{
    internal static class AddNewPageCommand
    {
        public static async Task Execute(bool shouldGenerateDataView)
        {
            string entityName = AnsiConsole.Prompt(
                new TextPrompt<string>("Entity name without spaces (e.g., YourEntityName):")
                    .PromptStyle("blue")
                    .ValidationErrorMessage("[red]Entity name can't be empty or contain spaces[/]")
                    .Validate(name =>
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return ValidationResult.Error("[red]Entity name can't be null or empty[/]");

                        if (name.Contains(" "))
                            return ValidationResult.Error("[red]Entity name can't have spaces[/]");

                        return ValidationResult.Success();
                    }));

            ConsoleHelper.MarkupLineLoading("Generating files for the entity...");

            string pagesFolderPath = GetPagesFolderPath();

            if (pagesFolderPath != null)
            {
                string kebabEntityName = entityName.ToKebabCase();

                string newPageFolderPath = Path.Combine(pagesFolderPath, kebabEntityName);
                if (Directory.Exists(newPageFolderPath))
                {
                    ConsoleHelper.MarkupLineWARNING($"Page folder already exists: {kebabEntityName}");
                }
                else
                {
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
            }

            ConsoleHelper.MarkupLineOK("Command execution completed!");
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
    }
}