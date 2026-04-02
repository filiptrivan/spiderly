using CaseConverter;
using Spectre.Console;
using Spiderly.CLI.Services;
using Spiderly.CLI.Services.Database.DbConnectionStringBuilder;
using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Commands
{
    internal static class InitCommand
    {
        private enum DbProviderChoiceCodes
        {
            PostgreSQL,
            SQLServer,
            Skip
        }

        public static async Task<int> Execute(bool isRunningFromNuget, string version, string appName = null, string dbProviderArg = null, string dbConnectionString = null, string packageManagerArg = null)
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

            PackageManagerCodes? packageManagerResult = GetPackageManager(packageManagerArg);
            if (packageManagerResult == null)
            {
                return 1;
            }

            PackageManagerCodes packageManager = packageManagerResult.Value;

            if (!await PrerequisiteChecker.ValidatePrerequisites(packageManager))
            {
                return 1;
            }

            if (skipDatabaseSetup && !string.IsNullOrWhiteSpace(dbConnectionString))
            {
                ConsoleHelper.MarkupLineWARNING("Both --db skip and --db-connection-string were provided. --db-connection-string takes priority.");
                skipDatabaseSetup = false;
            }

            if (skipDatabaseSetup)
            {
                ConsoleHelper.MarkupLineWARNING("Database setup skipped. You can configure the database later, but the app will not work until you set up the database, run migrations, and update the database.");
            }

            string currentPath = Environment.CurrentDirectory;

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasPmInstallErrors = false;
            bool hasUserSecretsErrors = false;

            string jwtKey = Helper.GenerateJwtSecretKey();
            string connectionString = null;

            if (!skipDatabaseSetup)
            {
                if (!string.IsNullOrWhiteSpace(dbConnectionString))
                {
                    connectionString = dbConnectionString;
                    ConsoleHelper.MarkupLineOK("Using provided connection string");
                }
                else
                {
                    BaseDbConnectionStringBuilder dbConnectionStringBuilder = GetDatabaseConnectionStringBuilder(dbProvider);
                    connectionString = await dbConnectionStringBuilder.CreateConnectionString(appName);

                    if (connectionString == null)
                    {
                        ConsoleHelper.MarkupLineERROR("Failed to connect to the database.");
                        return 1;
                    }
                }

                ConsoleHelper.MarkupLineOK($"Connected to database using connection string: [green]{connectionString}[/]");
            }

            try
            {
                ConsoleHelper.MarkupLineLoading("Generating files for the app...");
                ProjectGenerationOptions options = new()
                {
                    AppName = appName,
                    SpiderlyVersion = version,
                    IsRunningFromNuget = isRunningFromNuget,
                    DbProvider = dbProvider,
                    PackageManager = packageManager,
                };

                NetAndAngularFilesGenerator.Generate(currentPath, options);
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

            string installCommand = GetInstallCommand(packageManager);
            ConsoleHelper.MarkupLineLoading("Installing frontend packages...");
            (bool pmSuccess, string _) = await ProcessRunner.RunShellCommand(installCommand, frontendPath);
            if (pmSuccess)
            {
                ConsoleHelper.MarkupLineOK("Frontend packages installed successfully");
            }
            else
            {
                ConsoleHelper.MarkupLineERROR("Failed to install frontend packages");
                hasPmInstallErrors = true;
            }

            if (hasNetAndAngularInitErrors || hasUserSecretsErrors || hasRestoreErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasPmInstallErrors)
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
                else if (hasPmInstallErrors)
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

            if (!ConsoleHelper.IsInteractive())
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

            if (!ConsoleHelper.IsInteractive())
            {
                ConsoleHelper.MarkupLineERROR("Database provider is required in non-interactive mode. Use: --db postgresql, --db sqlserver, or --db skip");
                return null;
            }

            AnsiConsole.WriteLine();
            DbProviderChoiceCodes choice = AnsiConsole.Prompt(
                new SelectionPrompt<DbProviderChoiceCodes>()
                    .Title("Select database provider:")
                    .AddChoices(DbProviderChoiceCodes.PostgreSQL, DbProviderChoiceCodes.SQLServer, DbProviderChoiceCodes.Skip)
                    .UseConverter(c => c switch
                    {
                        DbProviderChoiceCodes.PostgreSQL => "PostgreSQL (recommended in most cases)",
                        DbProviderChoiceCodes.SQLServer => "SQL Server",
                        DbProviderChoiceCodes.Skip => "Skip database setup",
                        _ => throw new ArgumentOutOfRangeException(nameof(c), c, null)
                    }));

            return choice switch
            {
                DbProviderChoiceCodes.PostgreSQL => (DbProviderCodes.PostgreSQL, false),
                DbProviderChoiceCodes.SQLServer => (DbProviderCodes.SQLServer, false),
                DbProviderChoiceCodes.Skip => (DbProviderCodes.PostgreSQL, true),
                _ => throw new ArgumentOutOfRangeException(nameof(choice), choice, null)
            };
        }

        private static PackageManagerCodes? GetPackageManager(string packageManagerArg)
        {
            if (!string.IsNullOrWhiteSpace(packageManagerArg))
            {
                if (packageManagerArg.Equals("npm", StringComparison.OrdinalIgnoreCase))
                    return PackageManagerCodes.Npm;

                if (packageManagerArg.Equals("pnpm", StringComparison.OrdinalIgnoreCase))
                    return PackageManagerCodes.Pnpm;

                if (packageManagerArg.Equals("yarn", StringComparison.OrdinalIgnoreCase))
                    return PackageManagerCodes.Yarn;

                if (packageManagerArg.Equals("bun", StringComparison.OrdinalIgnoreCase))
                    return PackageManagerCodes.Bun;

                ConsoleHelper.MarkupLineERROR("Invalid package manager. Use 'npm', 'pnpm', 'yarn', or 'bun'");
                return null;
            }

            if (!ConsoleHelper.IsInteractive())
            {
                return PackageManagerCodes.Npm;
            }

            AnsiConsole.WriteLine();
            PackageManagerCodes choice = AnsiConsole.Prompt(
                new SelectionPrompt<PackageManagerCodes>()
                    .Title("Select package manager:")
                    .AddChoices(PackageManagerCodes.Npm, PackageManagerCodes.Pnpm, PackageManagerCodes.Yarn, PackageManagerCodes.Bun)
                    .UseConverter(c => c switch
                    {
                        PackageManagerCodes.Npm => "npm (default)",
                        _ => c.GetCommandName()
                    }));

            return choice;
        }

        private static string GetInstallCommand(PackageManagerCodes packageManager)
        {
            return $"{packageManager.GetCommandName()} install";
        }

        private static BaseDbConnectionStringBuilder GetDatabaseConnectionStringBuilder(DbProviderCodes dbProvider)
        {
            if (dbProvider == DbProviderCodes.SQLServer)
            {
                return new SQLServerConnectionStringBuilder();
            }

            return new PostgreSQLConnectionStringBuilder();
        }
    }
}