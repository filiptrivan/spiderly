using CaseConverter;
using Spiderly.CLI.Services;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Commands
{
    internal static class InitCommand
    {
        public static async Task Execute(bool hasTopMenu, bool isRunningFromNuget, string version)
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

            DbProviderCodes dbProvider;
            while (true)
            {
                Console.Write("\nSelect database provider:\n  1. SQL Server\n  2. PostgreSQL\nEnter choice (1 or 2): ");
                string dbChoice = Console.ReadLine();

                if (dbChoice == "1")
                {
                    dbProvider = DbProviderCodes.SQLServer;
                    break;
                }
                else if (dbChoice == "2")
                {
                    dbProvider = DbProviderCodes.PostgreSQL;
                    break;
                }
                else
                {
                    Console.WriteLine("Invalid choice. Please enter 1 or 2.");
                }
            }

            bool isMac = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
            if (isMac && dbProvider == DbProviderCodes.SQLServer)
            {
                Console.WriteLine("\n[WARNING] SQL Server is not officially supported on macOS.");
                Console.WriteLine("Please consider one of the following options:");
                Console.WriteLine("  1. Switch to PostgreSQL (recommended for macOS)");
                Console.WriteLine("  2. Use SQL Server via Docker");
                Console.WriteLine("\nTo use SQL Server via Docker, run:");
                Console.WriteLine("  docker run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourStrong@Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest");

                if (!ConsoleHelper.PromptYesNo("\nDo you want to continue with SQL Server? (y/n): "))
                {
                    Console.WriteLine("Exiting. Please rerun 'spiderly init' and select a different database provider.");
                    return;
                }
            }

            string currentPath = Environment.CurrentDirectory;

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;
            bool hasUserSecretsErrors = false;

            string jwtKey = Helper.GenerateJwtSecretKey();
            string connectionString = dbProvider == DbProviderCodes.SQLServer
                ? Helper.GetAvailableSqlServerConnectionString(appName)
                : Helper.GetAvailablePostgresConnectionString(appName);

            if (string.IsNullOrEmpty(connectionString))
            {
                string dbName = dbProvider == DbProviderCodes.SQLServer ? "SQL Server" : "PostgreSQL";
                Console.WriteLine($"\n[WARNING] No running {dbName} instance was detected.");

                if (ConsoleHelper.PromptYesNo($"\nWould you like to install {dbName} now? (y/n): "))
                {
                    bool installed = await DatabaseInstaller.InstallDatabase(dbProvider);
                    if (installed)
                    {
                        Console.WriteLine($"\n{dbName} has been installed successfully!");
                        Console.WriteLine("Please wait a moment for the service to start...");
                        await Task.Delay(5000);

                        connectionString = dbProvider == DbProviderCodes.SQLServer
                            ? Helper.GetAvailableSqlServerConnectionString(appName)
                            : Helper.GetAvailablePostgresConnectionString(appName);

                        if (string.IsNullOrEmpty(connectionString))
                        {
                            Console.WriteLine($"\n[WARNING] {dbName} was installed but is not responding yet.");
                            Console.WriteLine("Please start the service manually and rerun 'spiderly init'.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine($"\n[ERROR] Failed to install {dbName}.");
                        Console.WriteLine("Please install it manually and rerun 'spiderly init'.");
                        return;
                    }
                }
                else
                {
                    Console.WriteLine($"\nPlease ensure {dbName} is installed and running, then rerun 'spiderly init'.");

                    if (dbProvider == DbProviderCodes.SQLServer)
                    {
                        Console.WriteLine("\nTo install SQL Server manually:");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            Console.WriteLine("  Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
                        }
                        else
                        {
                            Console.WriteLine("  Use Docker: docker run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourStrong@Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest");
                        }
                    }
                    else
                    {
                        Console.WriteLine("\nTo install PostgreSQL manually:");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            Console.WriteLine("  Download from: https://www.postgresql.org/download/windows/");
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            Console.WriteLine("  Use Homebrew: brew install postgresql");
                            Console.WriteLine("  Or download from: https://www.postgresql.org/download/macosx/");
                        }
                        else
                        {
                            Console.WriteLine("  Use package manager or download from: https://www.postgresql.org/download/linux/");
                        }
                    }

                    return;
                }
            }

            Console.WriteLine("\nGenerating files for the app...");
            try
            {
                NetAndAngularFilesGenerator.Generate(currentPath, appName, version, isRunningFromNuget, primaryColor: null, hasTopMenu, jwtKey, connectionString, dbProvider);
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
                if (!await SetupUserSecrets(currentPath, appName, jwtKey, connectionString))
                {
                    Console.WriteLine("\n[ERROR] Failed to set up user secrets.");
                    hasUserSecretsErrors = true;
                }
            }

            string infrastructurePath = Path.Combine(currentPath, appName.ToKebabCase(), "Backend", $"{appName}.Infrastructure");
            string frontendPath = Path.Combine(currentPath, appName.ToKebabCase(), "Frontend");
            string infrastructureCsprojPath = Path.Combine(".", $"{appName}.Infrastructure.csproj");
            string webApiCsprojPath = Path.Combine("..", $"{appName}.WebAPI", $"{appName}.WebAPI.csproj");

            Console.WriteLine("\nGenerating the database migration...");
            string migrationArgs = $"ef migrations add InitialCreate --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            if (!await RunCommand("dotnet", migrationArgs, infrastructurePath))
            {
                Console.WriteLine("\n[ERROR] Failed to generate the database migration.");
                hasEfMigrationErrors = true;
            }

            Console.WriteLine("\nUpdating the database...");
            string updateArgs = $"ef database update --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            if (!await RunCommand("dotnet", updateArgs, infrastructurePath))
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
    }
}