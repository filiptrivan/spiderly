using Spectre.Console;
using Spiderly.CLI.Services.Database.OS;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public abstract class BaseDbConnectionStringBuilder
    {
        protected abstract string DbProviderName { get; }
        protected abstract string ManualInstallUrl { get; }
        protected BaseOSInstaller Installer { get; }

        protected BaseDbConnectionStringBuilder(BaseOSInstaller installer)
        {
            Installer = installer;
        }

        public async Task<string> CreateConnectionString(string appName)
        {
            ConsoleHelper.MarkupLineLoading($"Checking if your {DbProviderName} service is running...");

            bool isServiceRunning = await IsDatabaseServiceRunning();
            string connectionString;

            if (isServiceRunning)
            {
                ConsoleHelper.MarkupLineOK($"Your {DbProviderName} service is running.");

                ConsoleHelper.MarkupLineLoading($"Connecting to database...");
                connectionString = CreateDatabaseConnectionString(appName);

                if (connectionString == null)
                {

                    return null;
                }

                return connectionString;
            }

            if (
                ConsoleHelper.IsInteractive() &&
                ConsoleHelper.PromptYesNo($"We couldn't find running {DbProviderName} service. Would you like to install {DbProviderName} now?")
            )
            {
                bool installed = await InstallDatabaseProvider();
                if (installed)
                {
                    ConsoleHelper.MarkupLineOK($"{DbProviderName} has been installed successfully!");

                    await Task.Delay(3000);

                    ConsoleHelper.MarkupLineLoading($"Connecting to database...");

                    connectionString = CreateDatabaseConnectionString(appName);

                    if (connectionString == null)
                    {
                        ConsoleHelper.MarkupLineWARNING("We couldn't connect to a database.");
                        return null;
                    }

                    return connectionString;
                }

                ConsoleHelper.MarkupLineERROR($"{DbProviderName} installation failed.");
                return null;
            }

            AnsiConsole.MarkupLine($"Please ensure {DbProviderName} is installed and running, then rerun 'spiderly init' or run migrations on your own with EF Core.");
            AnsiConsole.MarkupLine($"To install {DbProviderName} manually:");
            AnsiConsole.MarkupLine($"  [dim]Download from: {ManualInstallUrl}[/]");

            return null;
        }

        protected abstract Task<bool> InstallDatabaseProvider();
        protected abstract string CreateDatabaseConnectionString(string appName);
        protected abstract Task<bool> IsDatabaseServiceRunning();
    }
}