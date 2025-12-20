using CaseConverter;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using System.Diagnostics;
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

        private static async Task Main(string[] args)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string fullVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            string version = fullVersion?.Split('+')[0]; // If we don't split, it will return the full version with the commit hash, which is not needed for the init command.

            if (args.HasArg("--help") || args.HasArg("-help") || args.HasArg("help"))
            {
                ShowHelp();
                return;
            }
            else if (args.HasArg("init"))
            {
                bool hasTopMenu = false;
                if (args.HasArg("--top-menu"))
                {
                    hasTopMenu = true;
                }

                bool IsRunningFromNuget = true;
                if (args.HasArg("--dev"))
                {
                    IsRunningFromNuget = false;
                }

                await Init(hasTopMenu, IsRunningFromNuget, version);
                return;
            }
            else if (args.HasArg("add-new-page"))
            {
                bool shouldGenerateDataView = false;
                if (args.HasArg("--data-view"))
                {
                    shouldGenerateDataView = true;
                }

                await AddNewPage(shouldGenerateDataView);
                return;
            }
            else if (args.Length == 0)
            {
                Console.WriteLine($$"""
           ____        _     _           _       
 ||  ||   / ___| _ __ (_) __| | ___ _ __| |_   _ 
 \\()//   \___ \| '_ \| |/ _` |/ _ \ '__| | | | |
//(__)\\   ___) | |_) | | (_| |  __/ |  | | |_| |
||    ||  |____/| .__/|_|\__,_|\___|_|  |_|\__, |
                |_|                        |___/ 
               
Spiderly.CLI v{{version}}
-------------------------------------------------
Type 'spiderly help' to see a list of available commands.
""");
            }
            else
            {
                Console.WriteLine("Unrecognized command. Type 'spiderly help' to see a list of available commands.");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  help                 Display this help message.");
            Console.WriteLine("  init                 Initialize a new project.");
            Console.WriteLine("  add-new-page         Generates starter files to support CRUD operations for a new entity.");
            Console.WriteLine();
            Console.WriteLine("Options for init:");
            Console.WriteLine("  --top-menu           Use a top menu layout instead of the default side menu layout.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  spiderly help");
            Console.WriteLine("  spiderly init");
            Console.WriteLine("  spiderly add-new-page");
        }

        #region Init

        private static async Task Init(bool hasTopMenu, bool IsRunningFromNuget, string version)
        {
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

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;
            bool hasUserSecretsErrors = false;

            string jwtKey = Helper.GenerateJwtSecretKey();
            string sqlServerConnectionString = Helper.GetAvailableSqlServerConnectionString(appName);

            Console.WriteLine("\nGenerating files for the app...");
            try
            {
                NetAndAngularFilesGenerator.Generate(currentPath, appName, version, IsRunningFromNuget, primaryColor: null, hasTopMenu, jwtKey, sqlServerConnectionString);
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

            if (!hasNetAndAngularInitErrors)
            {
                Console.WriteLine("\nSetting up user secrets...");
                if (!await SetupUserSecrets(currentPath, appName, jwtKey, sqlServerConnectionString))
                {
                    Console.WriteLine("\n[ERROR] Failed to set up user secrets.");
                    hasUserSecretsErrors = true;
                }
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

            if (hasNetAndAngularInitErrors || hasUserSecretsErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasNpmInstallErrors)
            {
                if (hasNetAndAngularInitErrors)
                {
                    Console.WriteLine("\nError occurred while generating files for the app.");
                }
                else if (hasUserSecretsErrors)
                {
                    Console.WriteLine("\nError occurred while setting up user secrets.");
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

        private static async Task<bool> SetupUserSecrets(string outputPath, string appName, string jwtKey, string sqlServerConnectionString)
        {
            string backendPath = Path.Combine(outputPath, appName.ToKebabCase(), "Backend", $"{appName}.WebAPI");

            bool success = true;

            if (!string.IsNullOrEmpty(jwtKey))
            {
                if (!await RunCommand("dotnet", $"user-secrets set \"AppSettings:Spiderly.Shared:JwtKey\" \"{jwtKey}\"", backendPath))
                {
                    success = false;
                }

                if (!await RunCommand("dotnet", $"user-secrets set \"AppSettings:Spiderly.Security:JwtKey\" \"{jwtKey}\"", backendPath))
                {
                    success = false;
                }
            }

            return success;
        }

        #endregion

        #region Add New Page

        private static async Task AddNewPage(bool shouldGenerateDataView)
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

        #endregion

        #region Helpers

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }

        #endregion
    }
}