using CaseConverter;
using Spectre.Console;
using Spiderly.CLI.Services;
using Spiderly.CLI.Services.Database.DbConnectionStringBuilder;
using Spiderly.CLI.Services.Database.OS;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Commands
{
    internal static class InitCommand
    {
        public static async Task<int> Execute(bool isRunningFromNuget, string version, string appName = null, string dbProviderArg = null)
        {
            appName = GetAppName(appName);
            if (appName == null)
            {
                return 1;
            }

            (DbProviderCodes provider, bool skipped)? dbProviderResult = GetDatabaseProvider(dbProviderArg);
            if (dbProviderResult == null)
            {
                return 1;
            }

            DbProviderCodes dbProvider = dbProviderResult.Value.provider;
            bool skipDatabaseSetup = dbProviderResult.Value.skipped;

            if (skipDatabaseSetup)
            {
                ConsoleHelper.MarkupLineWARNING("Database setup skipped. You can configure the database later, but the app will not work until you set up the database, run migrations, and update the database.");
            }

            string currentPath = Environment.CurrentDirectory;

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;
            bool hasUserSecretsErrors = false;

            string jwtKey = Helper.GenerateJwtSecretKey();
            string connectionString = null;

            if (!skipDatabaseSetup)
            {
                BaseOSInstaller osInstaller = GetOSInstaller(dbProvider);
                BaseDbConnectionStringBuilder databaseInstaller = GetDatabaseInstaller(dbProvider, osInstaller);
                connectionString = await databaseInstaller.CreateConnectionString(appName);

                if (connectionString == null)
                {
                    ConsoleHelper.MarkupLineERROR("Failed to connect to the database.");
                    return 1;
                }

                ConsoleHelper.MarkupLineOK($"Connected to database using connection string: [green]{connectionString}[/]");
            }

            try
            {
                ConsoleHelper.MarkupLineLoading("Generating files for the app...");
                NetAndAngularFilesGenerator.Generate(currentPath, appName, version, isRunningFromNuget, primaryColor: null, hasTopMenu: false, jwtKey, dbProvider);
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

            string rootPath = Path.Combine(currentPath, appName.ToKebabCase());

            if (!hasNetAndAngularInitErrors)
            {
                ConsoleHelper.MarkupLineLoading("Setting up user secrets...");
                if (await SetupUserSecrets(rootPath, appName, jwtKey, connectionString))
                {
                    ConsoleHelper.MarkupLineOK("User secrets configured successfully");
                }
                else
                {
                    hasUserSecretsErrors = true;
                }
            }
            string backendPath = Path.Combine(rootPath, "Backend");
            string frontendPath = Path.Combine(rootPath, "Frontend");
            string solutionPath = Path.Combine(backendPath, $"{appName}.sln");

            bool hasRestoreErrors = false;

            ConsoleHelper.MarkupLineLoading("Restoring NuGet packages...");
            (bool restoreSuccess, string _) = await ProcessRunner.RunCommand("dotnet", $"restore \"{solutionPath}\"", backendPath);
            if (restoreSuccess)
            {
                ConsoleHelper.MarkupLineOK("NuGet packages restored successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to restore NuGet packages");
                hasRestoreErrors = true;
            }

            if (!skipDatabaseSetup)
            {
                if (!await DotnetEfToolService.EnsureDotnetEfAvailable(backendPath))
                {
                    hasEfMigrationErrors = true;
                }

                if (!hasEfMigrationErrors)
                {
                    int migrationResult = await MigrationCommand.AddMigration("InitialCreate", backendPath);
                    if (migrationResult != 0)
                    {
                        hasEfMigrationErrors = true;
                    }
                }

                if (!hasEfMigrationErrors)
                {
                    int updateResult = await MigrationCommand.UpdateDatabase(backendPath);
                    if (updateResult != 0)
                    {
                        hasDatabaseUpdateErrors = true;
                    }
                }
            }

            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            ConsoleHelper.MarkupLineLoading("Installing frontend packages...");
            (bool npmSuccess, string _) = await ProcessRunner.RunCommand(npmCmd, npmArgs, frontendPath);
            if (npmSuccess)
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

                AnsiConsole.MarkupLine("Please fix the errors, then rerun the [blue]spiderly init[/] command using the same app name and location.");
                return 1;
            }
            else
            {
                ConsoleHelper.MarkupLineOK("App initialized successfully. Continue with the [blue]Open the Project[/] step in the documentation: [link]https://www.spiderly.dev/docs/getting-started#open-the-project[/]");
                Console.WriteLine();
                AnsiConsole.MarkupLine($"cd [blue]{appName.ToKebabCase()}[/]");
                AnsiConsole.MarkupLine("code .");
                Console.WriteLine();

                return 0;
            }
        }

        private static async Task<bool> SetupUserSecrets(string rootPath, string appName, string jwtKey, string connectionString)
        {
            string webApiPath = Path.Combine(rootPath, "Backend", $"{appName}.WebAPI");

            bool success = true;

            if (!string.IsNullOrEmpty(jwtKey))
            {
                string jwtKeyUserSecretsErrorMessage = "Failed to set jwt key in user secrets.";

                (bool sharedJwtSuccess, string _) = await ProcessRunner.RunCommand("dotnet", $"user-secrets set \"AppSettings:Spiderly.Shared:JwtKey\" \"{jwtKey}\"", webApiPath);
                if (!sharedJwtSuccess)
                {
                    ConsoleHelper.MarkupLineERROR(jwtKeyUserSecretsErrorMessage);
                    success = false;
                }

                (bool securityJwtSuccess, string _) = await ProcessRunner.RunCommand("dotnet", $"user-secrets set \"AppSettings:Spiderly.Security:JwtKey\" \"{jwtKey}\"", webApiPath);
                if (!securityJwtSuccess)
                {
                    ConsoleHelper.MarkupLineERROR(jwtKeyUserSecretsErrorMessage);
                    success = false;
                }
            }

            if (!string.IsNullOrEmpty(connectionString))
            {
                (bool connectionStringSuccess, string _) = await ProcessRunner.RunCommand("dotnet", $"user-secrets set \"AppSettings:Spiderly.Shared:ConnectionString\" \"{connectionString}\"", webApiPath);
                if (!connectionStringSuccess)
                {
                    ConsoleHelper.MarkupLineERROR($"Failed to set connection string in user secrets.");
                    success = false;
                }
            }

            return success;
        }

        private static string GetAppName(string appName)
        {
            if (!string.IsNullOrWhiteSpace(appName))
            {
                string validationError = ValidateAppName(appName);
                if (validationError != null)
                {
                    ConsoleHelper.MarkupLineERROR(validationError);
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
                new TextPrompt<string>("App name in PascalCase (e.g., YourAppName):")
                    .PromptStyle("blue")
                    .ValidationErrorMessage("[red]Invalid app name[/]")
                    .Validate(name =>
                    {
                        string error = ValidateAppName(name);
                        if (error != null)
                            return ValidationResult.Error($"[red]{error}[/]");

                        return ValidationResult.Success();
                    }));
        }

        private static string ValidateAppName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "App name can't be null or empty";

            if (name.Contains(' '))
                return "App name can't contain spaces";

            if (!char.IsUpper(name[0]))
                return "App name must start with an uppercase letter (PascalCase). Example: YourAppName";

            if (name.Contains('-'))
                return "App name must be in PascalCase. Example: YourAppName (your root folder will be created as your-app-name)";

            if (name.Contains('_'))
                return "App name must be in PascalCase without underscores. Example: YourAppName";

            return null;
        }

        private static (DbProviderCodes provider, bool skipped)? GetDatabaseProvider(string dbProviderArg)
        {
            if (!string.IsNullOrWhiteSpace(dbProviderArg))
            {
                if (dbProviderArg.Equals("postgresql", StringComparison.OrdinalIgnoreCase))
                    return (DbProviderCodes.PostgreSQL, false);

                if (dbProviderArg.Equals("sqlserver", StringComparison.OrdinalIgnoreCase))
                    return (DbProviderCodes.SQLServer, false);

                if (dbProviderArg.Equals("skip", StringComparison.OrdinalIgnoreCase))
                    return (DbProviderCodes.PostgreSQL, true);

                ConsoleHelper.MarkupLineERROR("Invalid database provider. Use 'postgresql', 'sqlserver', or 'skip'");
                return null;
            }

            if (!IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("Database provider is required in non-interactive mode. Use: --db postgresql, --db sqlserver, or --db skip");
                return null;
            }

            AnsiConsole.WriteLine();
            string choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select database provider:")
                    .AddChoices("PostgreSQL (recommended in most cases)", "SQL Server", "Skip database setup")
                    .UseConverter(c => c));

            return choice switch
            {
                "PostgreSQL (recommended in most cases)" => (DbProviderCodes.PostgreSQL, false),
                "SQL Server" => (DbProviderCodes.SQLServer, false),
                "Skip database setup" => (DbProviderCodes.PostgreSQL, true),
                _ => null
            };
        }

        private static bool IsInteractive()
        {
            return !Console.IsInputRedirected && Environment.UserInteractive;
        }

        private static BaseOSInstaller GetOSInstaller(DbProviderCodes dbProvider)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsInstaller(dbProvider);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                return new LinuxInstaller(dbProvider);
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacInstaller(dbProvider);
            }

            throw new PlatformNotSupportedException("Unsupported operating system");
        }

        private static BaseDbConnectionStringBuilder GetDatabaseInstaller(DbProviderCodes dbProvider, BaseOSInstaller osInstaller)
        {
            if (dbProvider == DbProviderCodes.SQLServer)
            {
                return new SQLServerConnectionStringBuilder(osInstaller);
            }

            return new PostgreSQLConnectionStringBuilder(osInstaller);
        }
    }
}