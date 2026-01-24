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
            ConsoleHelper.MarkupLineLoading(Installer.GetCheckingServiceMessage(DbProviderName));

            bool isServiceRunning = await IsDatabaseServiceRunning();
            string connectionString;

            if (isServiceRunning)
            {
                ConsoleHelper.MarkupLineOK(Installer.GetServiceRunningMessage(DbProviderName));

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
                ConsoleHelper.PromptYesNo(Installer.GetInstallPrompt(DbProviderName))
            )
            {
                bool installed = await InstallDatabaseProvider();
                if (installed)
                {
                    ConsoleHelper.MarkupLineOK($"{DbProviderName} has been installed successfully!");

                    connectionString = await TryCreateDatabaseConnectionString(appName);

                    if (connectionString == null)
                    {
                        return null;
                    }

                    return connectionString;
                }

                ConsoleHelper.MarkupLineERROR($"{DbProviderName} installation failed.");
                return null;
            }

            Installer.ShowDeclinedInstallMessage(DbProviderName, ManualInstallUrl);

            return null;
        }

        protected abstract Task<bool> InstallDatabaseProvider();
        protected abstract string CreateDatabaseConnectionString(string appName);
        protected abstract Task<bool> IsDatabaseServiceRunning();

        private async Task<string> TryCreateDatabaseConnectionString(string appName)
        {
            int[] delaysInSeconds = [3, 5, 10];

            for (int attempt = 0; attempt < delaysInSeconds.Length; attempt++)
            {
                await Task.Delay(delaysInSeconds[attempt] * 1000);

                ConsoleHelper.MarkupLineLoading($"Connecting to database (attempt {attempt + 1}/{delaysInSeconds.Length})...");

                string connectionString = CreateDatabaseConnectionString(appName);

                if (connectionString != null)
                {
                    return connectionString;
                }
            }

            return null;
        }
    }
}