using CaseConverter;
using Spectre.Console;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public abstract class BaseDbConnectionStringBuilder
    {
        protected abstract string DbProviderName { get; }
        protected abstract string DockerComposeContent { get; }
        protected abstract string ManualInstallUrl { get; }

        public async Task<string> CreateConnectionString(string appName)
        {
            ConsoleHelper.MarkupLineLoading($"Looking for a running {DbProviderName} instance...");

            string connectionString = CreateDatabaseConnectionString(appName);

            if (connectionString != null)
            {
                return connectionString;
            }

            bool isDockerAvailable = await IsDockerComposeAvailable();

            if (isDockerAvailable && ConsoleHelper.IsInteractive())
            {
                if (ConsoleHelper.PromptYesNo($"No running {DbProviderName} found. Install via Docker?"))
                {
                    if (await StartDockerCompose(appName))
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
                ConsoleHelper.MarkupLineERROR($"No running {DbProviderName} found. In non-interactive mode, start the database manually or run: docker compose up -d");
                return null;
            }

            ConsoleHelper.MarkupLineERROR($"No running {DbProviderName} found and Docker is not available.");
            AnsiConsole.MarkupLine($"Install {DbProviderName} from: [link]{ManualInstallUrl}[/]");
            AnsiConsole.MarkupLine("Or install Docker: [link]https://docs.docker.com/get-docker/[/]");
            return null;
        }

        protected abstract string CreateDatabaseConnectionString(string appName);

        private async Task<bool> IsDockerComposeAvailable()
        {
            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string shell = isWin ? "cmd.exe" : "/bin/bash";
            string args = isWin ? "/c docker compose version" : "-c \"docker compose version\"";

            return await ProcessRunner.IsCommandAvailable(shell, args);
        }

        private async Task<bool> StartDockerCompose(string appName)
        {
            string projectRoot = Path.Combine(Environment.CurrentDirectory, appName.ToKebabCase());
            string dockerComposeDir = Directory.Exists(projectRoot) ? projectRoot : Environment.CurrentDirectory;
            string dockerComposePath = Path.Combine(dockerComposeDir, "docker-compose.yml");

            if (!File.Exists(dockerComposePath))
            {
                ConsoleHelper.MarkupLineLoading("Generating docker-compose.yml...");
                File.WriteAllText(dockerComposePath, DockerComposeContent);
                ConsoleHelper.MarkupLineOK($"Created {dockerComposePath}");
            }

            ConsoleHelper.MarkupLineLoading($"Starting {DbProviderName} via Docker Compose...");

            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string shell = isWin ? "cmd.exe" : "/bin/bash";
            string args = isWin ? "/c docker compose up -d" : "-c \"docker compose up -d\"";

            (bool success, string _) = await ProcessRunner.RunCommand(shell, args, dockerComposeDir);

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
