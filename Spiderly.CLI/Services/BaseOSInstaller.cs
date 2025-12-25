using Spectre.Console;

namespace Spiderly.CLI.Services
{
    internal abstract class BaseOSInstaller : IOSInstaller
    {
        protected abstract string ShellFileName { get; }
        protected abstract string GetWhichCommand(string command);

        public abstract Task<bool> InstallPostgreSQL();
        public abstract Task<bool> InstallSqlServer();
        public abstract Task<bool> IsPostgreSQLServiceRunning();
        public abstract Task<bool> IsSqlServerServiceRunning();

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

        protected abstract Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand);

        protected void ShowManualInstallMessage(string displayName, string url)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"You can also install {displayName} manually:");
            AnsiConsole.MarkupLine($"  [link]{url}[/]");
        }
    }
}
