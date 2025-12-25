using CaseConverter;
using Spectre.Console;
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
        public static async Task<int> Execute(bool hasTopMenu, bool isRunningFromNuget, string version, string appName = null, string dbProviderArg = null)
        {
            appName = GetAppName(appName);
            if (appName == null)
            {
                return 1;
            }

            DbProviderCodes? dbProvider = GetDatabaseProvider(dbProviderArg);
            if (dbProvider == null)
            {
                return 1;
            }

            string currentPath = Environment.CurrentDirectory;

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;
            bool hasUserSecretsErrors = false;

            string jwtKey = Helper.GenerateJwtSecretKey();

            string dbName = dbProvider == DbProviderCodes.SQLServer ? "SQL Server" : "PostgreSQL";

            ConsoleHelper.MarkupLineLoading($"Connecting to {dbName}...");
            string connectionString = dbProvider == DbProviderCodes.SQLServer
                ? Helper.GetAvailableSqlServerConnectionString(appName)
                : Helper.GetAvailablePostgresConnectionString(appName);

            if (string.IsNullOrEmpty(connectionString))
            {
                ConsoleHelper.MarkupLineWARNING($"Could not establish a connection to {dbName}.");

                if (IsInteractive() && ConsoleHelper.PromptYesNo($"Would you like to install {dbName} now?"))
                {
                    bool installed = await DatabaseInstaller.InstallDatabase(dbProvider.Value);
                    if (installed)
                    {
                        ConsoleHelper.MarkupLineOK($"{dbName} has been installed successfully!");

                        ConsoleHelper.MarkupLineLoading($"Attempting to connect to {dbName}...");
                        await Task.Delay(5000);

                        connectionString = dbProvider == DbProviderCodes.SQLServer
                            ? Helper.GetAvailableSqlServerConnectionString(appName)
                            : Helper.GetAvailablePostgresConnectionString(appName);

                        if (string.IsNullOrEmpty(connectionString))
                        {
                            ConsoleHelper.MarkupLineWARNING($"Could not connect to {dbName}.");
                            AnsiConsole.WriteLine();
                            AnsiConsole.MarkupLine($"{dbName} was installed but connection failed. This could be due to authentication or service startup timing.");
                            AnsiConsole.WriteLine();

                            if (IsInteractive() && ConsoleHelper.PromptYesNo("Would you like to continue without a database connection? You can configure it later."))
                            {
                                connectionString = dbProvider == DbProviderCodes.SQLServer
                                    ? "Server=localhost;Database=" + appName + ";Integrated Security=true;"
                                    : "Host=localhost;Port=5432;Database=" + appName + ";Username=postgres;Password=postgres;";

                                ConsoleHelper.MarkupLineWARNING("Using placeholder connection string. You'll need to configure the database later.");
                            }
                            else
                            {
                                return 1;
                            }
                        }
                    }
                    else
                    {
                        ConsoleHelper.MarkupLineERROR($"{dbName} installation failed.");
                        return 1;
                    }
                }
                else
                {
                    ConsoleHelper.MarkupLineWARNING($"Please ensure {dbName} is installed and running, then rerun 'spiderly init'.");

                    if (dbProvider == DbProviderCodes.SQLServer)
                    {
                        AnsiConsole.MarkupLine("\nTo install SQL Server manually:");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                            AnsiConsole.MarkupLine("  [dim]Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads[/]");
                        else
                            AnsiConsole.MarkupLine("  [dim]Use Docker: docker run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourStrong@Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest[/]");
                    }
                    else
                    {
                        AnsiConsole.MarkupLine("\nTo install PostgreSQL manually:");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            AnsiConsole.MarkupLine("  [dim]Download from: https://www.postgresql.org/download/windows/[/]");
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            AnsiConsole.MarkupLine("  [dim]Use Homebrew: brew install postgresql[/]");
                            AnsiConsole.MarkupLine("  [dim]Or download from: https://www.postgresql.org/download/macosx/[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("  [dim]Use package manager or download from: https://www.postgresql.org/download/linux/[/]");
                        }
                    }

                    return 1;
                }
            }
            else
            {
                ConsoleHelper.MarkupLineOK(
                    $"Connected to database using connection string: [yellow]{connectionString}[/]"
                );
            }

            try
            {
                ConsoleHelper.MarkupLineLoading("Generating files for the app...");
                NetAndAngularFilesGenerator.Generate(currentPath, appName, version, isRunningFromNuget, primaryColor: null, hasTopMenu, jwtKey, connectionString, dbProvider.Value);
                ConsoleHelper.MarkupLineOK("Files generated successfully");
            }
            catch (Exception ex)
            {
                if (ex is BusinessException)
                    ConsoleHelper.MarkupLineERROR(ex.Message);
                else
                    ConsoleHelper.MarkupLineERROR(ex.ToString());

                hasNetAndAngularInitErrors = true;
            }

            if (!hasNetAndAngularInitErrors)
            {
                ConsoleHelper.MarkupLineLoading("Setting up user secrets...");
                if (await SetupUserSecrets(currentPath, appName, jwtKey, connectionString))
                {
                    ConsoleHelper.MarkupLineOK("User secrets configured successfully");
                }
                else
                {
                    ConsoleHelper.MarkupLineERROR("Failed to set up user secrets");
                    hasUserSecretsErrors = true;
                }
            }

            string backendPath = Path.Combine(currentPath, appName.ToKebabCase(), "Backend");
            string infrastructurePath = Path.Combine(backendPath, $"{appName}.Infrastructure");
            string frontendPath = Path.Combine(currentPath, appName.ToKebabCase(), "Frontend");
            string solutionPath = Path.Combine(backendPath, $"{appName}.sln");
            string infrastructureCsprojPath = Path.Combine(".", $"{appName}.Infrastructure.csproj");
            string webApiCsprojPath = Path.Combine("..", $"{appName}.WebAPI", $"{appName}.WebAPI.csproj");

            bool hasRestoreErrors = false;

            ConsoleHelper.MarkupLineLoading("Restoring NuGet packages...");
            if (await RunCommand("dotnet", $"restore \"{solutionPath}\"", backendPath))
            {
                ConsoleHelper.MarkupLineOK("NuGet packages restored successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to restore NuGet packages");
                hasRestoreErrors = true;
            }

            string migrationArgs = $"ef migrations add InitialCreate --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            ConsoleHelper.MarkupLineLoading("Generating the database migration...");
            if (await RunCommand("dotnet", migrationArgs, infrastructurePath))
            {
                ConsoleHelper.MarkupLineOK("Database migration generated successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to generate the database migration");
                hasEfMigrationErrors = true;
            }

            string updateArgs = $"ef database update --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            ConsoleHelper.MarkupLineLoading("Updating the database...");
            if (await RunCommand("dotnet", updateArgs, infrastructurePath))
            {
                ConsoleHelper.MarkupLineOK("Database updated successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to update the database");
                hasDatabaseUpdateErrors = true;
            }

            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            ConsoleHelper.MarkupLineLoading("Installing frontend packages...");
            if (await RunCommand(npmCmd, npmArgs, frontendPath))
            {
                ConsoleHelper.MarkupLineOK("Frontend packages installed successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to install frontend packages");
                hasNpmInstallErrors = true;
            }

            if (hasNetAndAngularInitErrors || hasUserSecretsErrors || hasRestoreErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasNpmInstallErrors)
            {
                if (hasNetAndAngularInitErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while generating files for the app.");
                }
                else if (hasUserSecretsErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while setting up user secrets.");
                }
                else if (hasRestoreErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while restoring NuGet packages.");
                }
                else if (hasEfMigrationErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while generating database migration.");
                }
                else if (hasDatabaseUpdateErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while initializing the database.");
                }
                else if (hasNpmInstallErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while installing frontend packages.");
                }

                AnsiConsole.MarkupLine("Please fix the errors, then rerun the 'spiderly init' command using the same app name and location.");
                return 1;
            }
            else
            {
                ConsoleHelper.MarkupLineOK("App initialized successfully!");
                AnsiConsole.MarkupLine("Continue with Step 4 from the getting started guide: [link]https://www.spiderly.dev/docs/getting-started[/]");
                return 0;
            }
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

        private static async Task<bool> RunCommand(
            string fileName,
            string arguments,
            string workingDirectory)
        {
            using (Process process = new Process())
            {
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        Console.WriteLine(e.Data); // We shouldn't use AnsiConsole here because of the unexpected markup
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        Console.WriteLine(e.Data); // We shouldn't use AnsiConsole here because of the unexpected markup
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
        }

        private static string GetAppName(string appName)
        {
            if (!string.IsNullOrWhiteSpace(appName))
            {
                if (appName.Contains(" "))
                {
                    ConsoleHelper.MarkupLineERROR("App name can't contain spaces");
                    return null;
                }
                return appName;
            }

            if (!IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("App name is required in non-interactive mode. Use: spiderly init --name YourAppName");
                return null;
            }

            return AnsiConsole.Prompt(
                new TextPrompt<string>("App name without spaces (e.g., YourAppName):")
                    .PromptStyle("blue")
                    .ValidationErrorMessage("[red]App name can't be empty or contain spaces[/]")
                    .Validate(name =>
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            return ValidationResult.Error("[red]App name can't be null or empty[/]");

                        if (name.Contains(" "))
                            return ValidationResult.Error("[red]App name can't have spaces[/]");

                        return ValidationResult.Success();
                    }));
        }

        private static DbProviderCodes? GetDatabaseProvider(string dbProviderArg)
        {
            if (!string.IsNullOrWhiteSpace(dbProviderArg))
            {
                if (dbProviderArg.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
                {
                    return DbProviderCodes.SQLServer;
                }

                if (dbProviderArg.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
                {
                    return DbProviderCodes.PostgreSQL;
                }

                ConsoleHelper.MarkupLineERROR("Invalid database provider. Use 'sqlserver' or 'postgresql'");
                return null;
            }

            if (!IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("Database provider is required in non-interactive mode. Use: --db sqlserver or --db postgresql");
                return null;
            }

            AnsiConsole.WriteLine();
            return AnsiConsole.Prompt(
                new SelectionPrompt<DbProviderCodes>()
                    .Title("Select database provider:")
                    .AddChoices(DbProviderCodes.SQLServer, DbProviderCodes.PostgreSQL)
                    .UseConverter(choice => choice == DbProviderCodes.SQLServer ? "SQL Server" : "PostgreSQL"));
        }

        private static bool IsInteractive()
        {
            return !Console.IsInputRedirected && Environment.UserInteractive;
        }
    }
}