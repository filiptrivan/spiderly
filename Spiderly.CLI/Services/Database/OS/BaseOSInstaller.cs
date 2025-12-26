using Spectre.Console;
using Spiderly.Shared.Enums;

namespace Spiderly.CLI.Services.Database.OS
{
    public abstract class BaseOSInstaller
    {
        public DbProviderCodes DbProvider { get; set; }
        protected abstract string ShellFileName { get; }
        protected abstract string GetWhichCommand(string command);
        public abstract Task<bool> InstallPostgreSQL();
        public abstract Task<bool> InstallSqlServer();
        public abstract Task<bool> IsPostgreSQLServiceRunning();
        public abstract Task<bool> IsSqlServerServiceRunning();
        protected abstract Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand);

        public BaseOSInstaller(DbProviderCodes dbProvider)
        {
            DbProvider = dbProvider;
        }

        public Task<bool> IsCommandAvailable(string command)
        {
            return ProcessRunner.IsCommandAvailable(ShellFileName, GetWhichCommand(command));
        }

        protected async Task ConfigurePostgreSQLAuthentication()
        {
            ConsoleHelper.MarkupLineLoading("Configuring PostgreSQL authentication...");

            string alterUserCommand = "ALTER USER postgres WITH PASSWORD 'postgres';";
            (string command, string arguments) = await GetPsqlCommandAsync(alterUserCommand);

            if (!string.IsNullOrEmpty(command))
            {
                await ProcessRunner.RunCommand(command, arguments);
            }
            else
            {
                ConsoleHelper.MarkupLineWARNING("Could not locate psql. Skipping automatic authentication configuration.");
            }
        }

        protected static void ShowManualInstallMessage(string displayName, string url)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"You can also install {displayName} manually:");
            AnsiConsole.MarkupLine($"  [link]{url}[/]");
        }
    }
}
