using Spectre.Console;

namespace Spiderly.CLI.Services
{
    internal class MacInstaller : BaseOSInstaller
    {
        protected override string ShellFileName => "/bin/bash";
        protected override string GetWhichCommand(string command) => $"-c \"which {command}\"";

        protected override async Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand)
        {
            return ("psql", $"-U postgres -c \"{sqlCommand}\"");
        }

        public override async Task<bool> InstallPostgreSQL()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on macOS...");

            if (!await IsCommandAvailable("brew") && !await PromptAndInstallHomebrew())
            {
                ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/macosx/");
                return false;
            }

            if (await InstallViaHomebrew("postgresql", "PostgreSQL"))
            {
                await ProcessRunner.RunCommand("brew", "services start postgresql");
                await ConfigurePostgreSQLAuthentication();
                return true;
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/macosx/");
            return false;
        }

        public override async Task<bool> InstallSqlServer()
        {
            Console.WriteLine("\nSQL Server requires Docker on macOS.");
            return await DockerInstaller.InstallSqlServerDocker(this, InstallDocker);
        }

        private async Task<bool> InstallViaHomebrew(string packageName, string displayName)
        {
            Console.WriteLine($"\nHomebrew detected. Installing {displayName}...");
            Console.WriteLine("This may take several minutes...");

            (bool installed, _) = await ProcessRunner.RunCommand("brew", $"install {packageName}");
            if (installed)
            {
                ConsoleHelper.MarkupLineOK($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private async Task<bool> PromptAndInstallHomebrew()
        {
            AnsiConsole.WriteLine();
            ConsoleHelper.MarkupLineWARNING("Homebrew is not installed.");

            if (ConsoleHelper.PromptYesNo("Would you like to install Homebrew package manager?"))
            {
                if (await InstallHomebrew())
                {
                    ConsoleHelper.MarkupLineOK("Homebrew installed successfully!");
                    return true;
                }
                else
                {
                    ConsoleHelper.MarkupLineERROR("Failed to install Homebrew.");
                    return false;
                }
            }

            return false;
        }

        private async Task<bool> InstallHomebrew()
        {
            Console.WriteLine("\nInstalling Homebrew package manager...");
            Console.WriteLine("This may take several minutes...");

            string installScript = "/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\"";

            (bool successful, string _) = await ProcessRunner.RunCommand("/bin/bash", $"-c \"{installScript}\"");
            return successful;
        }

        public override async Task<bool> IsPostgreSQLServiceRunning()
        {
            (bool success, string output) = await ProcessRunner.RunCommand("brew", "services list");
            if (success && output.Contains("postgresql") && output.Contains("started"))
                return true;

            (bool dockerSuccess, string dockerOutput) = await ProcessRunner.RunCommand("docker", "ps --filter name=postgres --filter status=running --format '{{.Names}}'");
            return dockerSuccess && !string.IsNullOrWhiteSpace(dockerOutput);
        }

        public override async Task<bool> IsSqlServerServiceRunning()
        {
            (bool dockerSuccess, string dockerOutput) = await ProcessRunner.RunCommand("docker", "ps --filter name=sqlserver --filter status=running --format '{{.Names}}'");
            return dockerSuccess && !string.IsNullOrWhiteSpace(dockerOutput);
        }

        private async Task<bool> InstallDocker()
        {
            Console.WriteLine("\nAttempting to install Docker on macOS...");

            if (!await IsCommandAvailable("brew") && !await PromptAndInstallHomebrew())
            {
                ShowManualInstallMessage("Docker", "https://www.docker.com/get-started");
                return false;
            }

            if (await InstallViaHomebrew("--cask docker", "Docker Desktop"))
            {
                ConsoleHelper.MarkupLineOK("Docker Desktop has been installed.");
                Console.WriteLine("Please start Docker Desktop from your Applications folder before continuing.");
                return true;
            }

            ShowManualInstallMessage("Docker", "https://www.docker.com/get-started");
            return false;
        }
    }
}
