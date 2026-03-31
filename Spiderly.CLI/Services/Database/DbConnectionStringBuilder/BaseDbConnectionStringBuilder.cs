using Spectre.Console;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public abstract class BaseDbConnectionStringBuilder
    {
        protected abstract string DbProviderName { get; }
        protected abstract string DockerRunArguments { get; }
        protected abstract string ManualInstallUrl { get; }

        public async Task<string> CreateConnectionString(string appName)
        {
            ConsoleHelper.MarkupLineLoading($"Looking for a running {DbProviderName} instance...");

            string connectionString = CreateDatabaseConnectionString(appName);

            if (connectionString != null)
            {
                return connectionString;
            }

            bool isDockerAvailable = await IsDockerAvailable();

            if (isDockerAvailable && ConsoleHelper.IsInteractive())
            {
                if (ConsoleHelper.PromptYesNo($"No running {DbProviderName} found. Install via Docker?"))
                {
                    if (await StartDockerContainer())
                    {
                        connectionString = await TryCreateDatabaseConnectionString(appName);
                        if (connectionString != null)
                        {
                            return connectionString;
                        }
                    }

                    ConsoleHelper.MarkupLineERROR($"Failed to start {DbProviderName} via Docker.");
                    return null;
                }
            }
            else if (isDockerAvailable)
            {
                ConsoleHelper.MarkupLineLoading($"No running {DbProviderName} found. Starting via Docker (non-interactive mode)...");

                if (await StartDockerContainer())
                {
                    connectionString = await TryCreateDatabaseConnectionString(appName);
                    if (connectionString != null)
                    {
                        return connectionString;
                    }
                }

                ConsoleHelper.MarkupLineERROR($"Failed to start {DbProviderName} via Docker. Use --db-connection-string to provide a connection string, or --db skip to skip database setup.");
                return null;
            }

            ConsoleHelper.MarkupLineERROR($"No running {DbProviderName} found and Docker is not available.");
            AnsiConsole.MarkupLine($"Install {DbProviderName} from: [link]{ManualInstallUrl}[/]");
            AnsiConsole.MarkupLine("Or install Docker: [link]https://docs.docker.com/get-docker/[/]");
            return null;
        }

        protected abstract string CreateDatabaseConnectionString(string appName);

        private async Task<bool> IsDockerAvailable()
        {
            (bool success, string _) = await ProcessRunner.RunShellCommand("docker version");
            return success;
        }

        private async Task<bool> StartDockerContainer()
        {
            ConsoleHelper.MarkupLineLoading($"Starting {DbProviderName} via Docker...");

            (bool success, string _) = await ProcessRunner.RunShellCommand($"docker {DockerRunArguments}");

            if (success)
            {
                ConsoleHelper.MarkupLineOK($"{DbProviderName} container started.");
            }

            return success;
        }

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
