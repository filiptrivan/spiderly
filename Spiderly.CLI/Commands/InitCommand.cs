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
        public static async Task Execute(bool hasTopMenu, bool isRunningFromNuget, string version)
        {
            string appName = AnsiConsole.Prompt(
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

            AnsiConsole.WriteLine();
            DbProviderCodes dbProvider = AnsiConsole.Prompt(
                new SelectionPrompt<DbProviderCodes>()
                    .Title("Select database provider:")
                    .AddChoices(DbProviderCodes.SQLServer, DbProviderCodes.PostgreSQL)
                    .UseConverter(choice => choice == DbProviderCodes.SQLServer ? "SQL Server" : "PostgreSQL"));

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
                AnsiConsole.WriteLine();
                ConsoleHelper.MarkupLineWARNING($"No running {dbName} instance was detected.");

                if (ConsoleHelper.PromptYesNo($"Would you like to install {dbName} now?"))
                {
                    bool installed = await DatabaseInstaller.InstallDatabase(dbProvider);
                    if (installed)
                    {
                        ConsoleHelper.MarkupLineOK($"{dbName} has been installed successfully!");

                        await AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .StartAsync("Waiting for the service to start...", async ctx =>
                            {
                                await Task.Delay(5000);
                            });

                        connectionString = dbProvider == DbProviderCodes.SQLServer
                            ? Helper.GetAvailableSqlServerConnectionString(appName)
                            : Helper.GetAvailablePostgresConnectionString(appName);

                        if (string.IsNullOrEmpty(connectionString))
                        {
                            ConsoleHelper.MarkupLineWARNING($"{dbName} was installed but is not responding yet.");
                            AnsiConsole.MarkupLine("Please start the service manually and rerun 'spiderly init'.");
                            return;
                        }
                    }
                    else
                    {
                        ConsoleHelper.MarkupLineERROR($"Failed to install {dbName}.");
                        AnsiConsole.MarkupLine("Please install it manually and rerun 'spiderly init'.");
                        return;
                    }
                }
                else
                {
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine($"Please ensure {dbName} is installed and running, then rerun 'spiderly init'.");

                    if (dbProvider == DbProviderCodes.SQLServer)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[cyan]To install SQL Server manually:[/]");
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            AnsiConsole.MarkupLine("  [dim]Download from: https://www.microsoft.com/en-us/sql-server/sql-server-downloads[/]");
                        }
                        else
                        {
                            AnsiConsole.MarkupLine("  [dim]Use Docker: docker run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourStrong@Passw0rd\" -p 1433:1433 -d mcr.microsoft.com/mssql/server:2022-latest[/]");
                        }
                    }
                    else
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[cyan]To install PostgreSQL manually:[/]");
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

                    return;
                }
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .Start("Generating files for the app...", ctx =>
                {
                    try
                    {
                        NetAndAngularFilesGenerator.Generate(currentPath, appName, version, isRunningFromNuget, primaryColor: null, hasTopMenu, jwtKey, connectionString, dbProvider);
                        ConsoleHelper.MarkupLineOK("Files generated successfully");
                    }
                    catch (Exception ex)
                    {
                        if (ex is BusinessException)
                        {
                            ConsoleHelper.MarkupLineERROR($"Error occurred:\n{ex.Message}");
                        }
                        else
                        {
                            ConsoleHelper.MarkupLineERROR($"Error occurred:\n{ex}");
                        }

                        hasNetAndAngularInitErrors = true;
                    }
                });

            if (!hasNetAndAngularInitErrors)
            {
                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Setting up user secrets...", async ctx =>
                    {
                        if (!await SetupUserSecrets(currentPath, appName, jwtKey, connectionString))
                        {
                            ConsoleHelper.MarkupLineERROR("Failed to set up user secrets");
                            hasUserSecretsErrors = true;
                        }
                        else
                        {
                            ConsoleHelper.MarkupLineOK("User secrets configured successfully");
                        }
                    });
            }

            string infrastructurePath = Path.Combine(currentPath, appName.ToKebabCase(), "Backend", $"{appName}.Infrastructure");
            string frontendPath = Path.Combine(currentPath, appName.ToKebabCase(), "Frontend");
            string infrastructureCsprojPath = Path.Combine(".", $"{appName}.Infrastructure.csproj");
            string webApiCsprojPath = Path.Combine("..", $"{appName}.WebAPI", $"{appName}.WebAPI.csproj");

            string migrationArgs = $"ef migrations add InitialCreate --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Generating the database migration...", async ctx =>
                {
                    if (!await RunCommand("dotnet", migrationArgs, infrastructurePath))
                    {
                        ConsoleHelper.MarkupLineERROR("Failed to generate the database migration");
                        hasEfMigrationErrors = true;
                    }
                    else
                    {
                        ConsoleHelper.MarkupLineOK("Database migration generated successfully");
                    }
                });

            string updateArgs = $"ef database update --project {infrastructureCsprojPath} --startup-project {webApiCsprojPath}";
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Updating the database...", async ctx =>
                {
                    if (!await RunCommand("dotnet", updateArgs, infrastructurePath))
                    {
                        ConsoleHelper.MarkupLineERROR("Failed to update the database");
                        hasDatabaseUpdateErrors = true;
                    }
                    else
                    {
                        ConsoleHelper.MarkupLineOK("Database updated successfully");
                    }
                });

            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Installing frontend packages...", async ctx =>
                {
                    if (!await RunCommand(npmCmd, npmArgs, frontendPath))
                    {
                        ConsoleHelper.MarkupLineERROR("Failed to install frontend packages");
                        hasNpmInstallErrors = true;
                    }
                    else
                    {
                        ConsoleHelper.MarkupLineOK("Frontend packages installed successfully");
                    }
                });

            AnsiConsole.WriteLine();
            if (hasNetAndAngularInitErrors || hasUserSecretsErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasNpmInstallErrors)
            {
                if (hasNetAndAngularInitErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while generating files for the app.");
                }
                else if (hasUserSecretsErrors)
                {
                    ConsoleHelper.MarkupLineERROR("Error occurred while setting up user secrets.");
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

                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Please fix the errors, then rerun the 'spiderly init' command using the same app name and location.");
            }
            else
            {
                ConsoleHelper.MarkupLineOK("App initialized successfully!");
                AnsiConsole.MarkupLine("Continue with Step 4 from the getting started guide: [link]https://www.spiderly.dev/docs/getting-started[/]");
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