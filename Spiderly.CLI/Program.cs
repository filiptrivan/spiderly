using CaseConverter;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml.FormulaParsing.Excel.Functions.DateTime;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Spiderly.CLI
{
    /// <summary>
    /// The main entry point for the Spiderly command-line interface (CLI) tool.
    /// This class handles parsing command-line arguments, displaying help information,
    /// and executing commands such as initializing a new Spiderly project structure
    /// with a .NET backend and an Angular frontend.
    /// </summary>
    internal static class Program
    {
        private static readonly char _s_ = Path.DirectorySeparatorChar;

        static async Task Main(string[] args)
        {
            if (args.HasArg("--help") || args.HasArg("-help") || args.HasArg("help"))
            {
                ShowHelp();
                return;
            }

            if (args.HasArg("init"))
            {
                await Init();
                return;
            }

            if (args.HasArg("add-new-page"))
            {
                await AddNewPage();
                return;
            }

            Console.WriteLine("\nUnrecognized command. Type 'spiderly help' to see a list of available commands.");


            //IConfiguration config = new ConfigurationBuilder()
            //    .AddCommandLine(args)
            //    .Build();

            //string initType = config["init"];
            //string primaryColor = config["primary-color"];
            //string appName = config["app-name"];
            //string currentPath = Environment.CurrentDirectory;
        }


        private static void ShowHelp()
        {
            Console.WriteLine("Usage: [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  help                 Display this help message.");
            Console.WriteLine("  init                 Initialize a new project.");
            Console.WriteLine("  add-new-page         Generates starter files to support CRUD operations.");
            Console.WriteLine();
            Console.WriteLine("Options for init:");
            Console.WriteLine("  app-name             Specify the name of the application (no spaces allowed).");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  spiderly help");
            Console.WriteLine("  spiderly init");
            Console.WriteLine("  spiderly add-new-page");
        }

        #region Init

        private static async Task Init()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string appName;

            while (true)
            {
                Console.Write("App name without spaces (e.g., YourAppName): ");
                appName = Console.ReadLine();

                if (string.IsNullOrEmpty(appName))
                {
                    Console.WriteLine("Your app name can't be null or empty.");
                    continue;
                }

                if (appName.Contains(" "))
                {
                    Console.WriteLine("Your app name can't have spaces.");
                    continue;
                }

                break;
            }

            string currentPath = Environment.CurrentDirectory;

            string version = assembly.GetName().Version.ToString();

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;

            Console.WriteLine("\nGenerating files for the app...");
            try
            {
                NetAndAngularFilesGenerator.Generate(currentPath, appName, version, isFromNuget: true, null);
                Console.WriteLine("Finished generating files for the app.");
            }
            catch (Exception ex)
            {
                if (ex is BusinessException)
                {
                    Console.WriteLine($"[ERROR] Error occurred:\n{ex.Message}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Error occurred:\n{ex}");
                }

                hasNetAndAngularInitErrors = true;
            }

            string infrastructurePath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}Backend{_s_}{appName}.Infrastructure");
            string frontendPath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}Frontend");

            Console.WriteLine("\nGenerating the database migration...");
            if (!await RunCommand("dotnet", @$"ef migrations add InitialCreate --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\n[ERROR] Failed to generate the database migration.");
                hasEfMigrationErrors = true;
            }

            Console.WriteLine("\nUpdating the database...");
            if (!await RunCommand("dotnet", @$"ef database update --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\n[ERROR] Failed to update the database.");
                hasDatabaseUpdateErrors = true;
            }

            Console.WriteLine("\nInstalling frontend packages...");
            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            if (!await RunCommand(npmCmd, npmArgs, frontendPath))
            {
                Console.WriteLine("\n[ERROR] Failed to install frontend packages.");
                hasNpmInstallErrors = true;
            }

            if (hasNetAndAngularInitErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasNpmInstallErrors)
            {
                if (hasNetAndAngularInitErrors)
                {
                    Console.WriteLine("\nError occurred while generating files for the app.");
                }
                else if (hasEfMigrationErrors)
                {
                    Console.WriteLine("\nError occurred while generating database migration.");
                }
                else if (hasDatabaseUpdateErrors)
                {
                    Console.WriteLine("\nError occurred while initializing the database.");
                }
                else if (hasNpmInstallErrors)
                {
                    Console.WriteLine("\nError occurred while installing frontend packages.");
                }

                Console.WriteLine("\nPlease fix the errors, then rerun the 'spiderly init' command using the same app name and location.");
            }
            else
            {
                Console.WriteLine("\nApp initialized successfully, continue with the Step 4 from the getting started guide!");
            }
        }

        private static async Task<bool> RunCommand(string fileName, string arguments, string workingDirectory)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = false
                },
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += (sender, e) => { if (e.Data != null) Console.WriteLine(e.Data); };
            process.ErrorDataReceived += (sender, e) => { if (e.Data != null) Console.Error.WriteLine(e.Data); };

            process.Start();

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            return process.ExitCode == 0;
        }

        #endregion

        #region Add New Page

        private static async Task AddNewPage()
        {
            string entityName = null;

            while (true)
            {
                Console.Write("Entity name without spaces (e.g., YourNewEntityName): ");
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

            string rootPath = Directory.GetCurrentDirectory();

            string[] folders = rootPath.Split('\\');
            string kebabAppName = folders[folders.Length - 1];
            string appName = kebabAppName.ToPascalCase();

            Console.WriteLine("\nGenerating files for the entity...");

            string backendControllersPath = Path.Combine(rootPath, "Backend", $"{appName}.WebAPI", "Controllers");
            if (!Directory.Exists(backendControllersPath))
            {
                Console.WriteLine($"\n[WARNING] Controllers folder not found: {backendControllersPath}");
            }
            else
            {
                string controllerFileName = $"{entityName}Controller.cs";
                string controllerFilePath = Path.Combine(backendControllersPath, controllerFileName);

                if (File.Exists(controllerFilePath))
                {
                    Console.WriteLine($"\n[WARNING] Controller file already exists: {controllerFileName}");
                }
                else
                {
                    string controllerTemplate = NetAndAngularFilesGenerator.GetSpiderlyControllerTemplate(entityName, appName);
                    await File.WriteAllTextAsync(controllerFilePath, controllerTemplate, Encoding.UTF8);
                    Console.WriteLine($"\nController successfully generated: {controllerFilePath}");
                }
            }

            string pagesFolderPath = Path.Combine(rootPath, "Frontend", "src", "app", "pages");
            if (!Directory.Exists(pagesFolderPath))
            {
                Console.WriteLine($"\n[WARNING] Pages folder not found: {pagesFolderPath}");
            }
            else
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

                    string tableTsPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-table.component.ts");
                    string tableTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularTableTsTemplate(entityName);
                    await File.WriteAllTextAsync(tableTsPath, tableTsTemplate, Encoding.UTF8);
                    Console.WriteLine($"Table ts file successfully generated: {tableTsPath}");

                    string tableHtmlPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-table.component.html");
                    string tableHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularTableHtmlTemplate(entityName);
                    await File.WriteAllTextAsync(tableHtmlPath, tableHtmlTemplate, Encoding.UTF8);
                    Console.WriteLine($"Table html file successfully generated: {tableHtmlPath}");

                    string detailsTsPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.ts");
                    string detailsTsTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsTsTemplate(entityName);
                    await File.WriteAllTextAsync(detailsTsPath, detailsTsTemplate, Encoding.UTF8);
                    Console.WriteLine($"Details ts successfully generated: {detailsTsPath}");

                    string detailsHtmlPath = Path.Combine(newPageFolderPath, $"{kebabEntityName}-details.component.html");
                    string detailsHtmlTemplate = NetAndAngularFilesGenerator.GetSpiderlyAngularDetailsHtmlTemplate(entityName);
                    await File.WriteAllTextAsync(detailsHtmlPath, detailsHtmlTemplate, Encoding.UTF8);
                    Console.WriteLine($"Details html successfully generated: {detailsHtmlPath}");
                }
            }

            Console.WriteLine("\nCommand execution completed.");
        }

        #endregion

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }
    }
}