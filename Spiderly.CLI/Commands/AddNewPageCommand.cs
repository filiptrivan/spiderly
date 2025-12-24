using CaseConverter;
using Spiderly.Shared.Helpers;
using System.Text;

namespace Spiderly.CLI.Commands
{
    internal static class AddNewPageCommand
    {
        public static async Task Execute(bool shouldGenerateDataView)
        {
            string entityName;

            while (true)
            {
                Console.Write("Entity name without spaces (e.g., YourEntityName): ");
                entityName = Console.ReadLine();

                if (string.IsNullOrEmpty(entityName))
                {
                    Console.WriteLine("Entity name can't be null or empty.");
                    continue;
                }

                if (entityName.Contains(" "))
                {
                    Console.WriteLine("Entity name can't have spaces.");
                    continue;
                }

                break;
            }

            Console.WriteLine("\nGenerating files for the entity...");

            string pagesFolderPath = GetPagesFolderPath();

            if (pagesFolderPath != null)
            {
                string kebabEntityName = entityName.ToKebabCase();

                string newPageFolderPath = Path.Combine(pagesFolderPath, kebabEntityName);
                if (Directory.Exists(newPageFolderPath))
                {
                    Console.WriteLine($"\n[WARNING] Page folder already exists: {kebabEntityName}");
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
                    Console.WriteLine($"\nList .ts file successfully generated: {listTsPath}");

                    await File.WriteAllTextAsync(listHtmlPath, listHtmlTemplate, Encoding.UTF8);
                    Console.WriteLine($"\nList .html file successfully generated: {listHtmlPath}");

                    string detailsTsPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.ts");
                    string detailsTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsTsTemplate(entityName);
                    await File.WriteAllTextAsync(detailsTsPath, detailsTsTemplate, Encoding.UTF8);
                    Console.WriteLine($"\nDetails .ts successfully generated: {detailsTsPath}");

                    string detailsHtmlPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.html");
                    string detailsHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsHtmlTemplate(entityName);
                    await File.WriteAllTextAsync(detailsHtmlPath, detailsHtmlTemplate, Encoding.UTF8);
                    Console.WriteLine($"\nDetails .html successfully generated: {detailsHtmlPath}");
                }
            }

            Console.WriteLine("\nCommand execution completed.");
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

            Console.WriteLine($$"""
[ERROR] Expected frontend project structure was not detected.
Tried the following paths:
{{string.Join(Environment.NewLine, candidatePaths)}}
""");

            return null;
        }
    }
}