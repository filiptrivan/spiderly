using Spiderly.Shared.Enums;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.OS
{
    internal class LinuxInstaller : BaseOSInstaller
    {
        protected override string ShellFileName => "/bin/bash";
        protected override string GetWhichCommand(string command) => $"-c \"which {command}\"";

        public LinuxInstaller(DbProviderCodes dbProvider) : base(dbProvider)
        {
        }

        protected override Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand)
        {
            return Task.FromResult(("sudo", $"-u postgres psql -c \"{sqlCommand}\""));
        }

        public override async Task<bool> InstallPostgreSQL()
        {
            return await InstallPostgreSQLViaDocker();
        }

        public override async Task<bool> InstallSqlServer()
        {
            ConsoleHelper.MarkupLineLoading("SQL Server requires Docker on Linux...");
            return await DockerInstaller.InstallSqlServerDocker(this, InstallDocker);
        }

        public override async Task<bool> IsPostgreSQLServiceRunning()
        {
            (bool dockerSuccess, string dockerOutput) = await ProcessRunner.RunCommand("sudo", "docker ps --filter name=spiderly-postgres --filter status=running --format '{{.Names}}'");
            return dockerSuccess && !string.IsNullOrWhiteSpace(dockerOutput);
        }

        public override async Task<bool> IsSqlServerServiceRunning()
        {
            (bool dockerSuccess, string dockerOutput) = await ProcessRunner.RunCommand("sudo", "docker ps --filter name=spiderly-sqlserver --filter status=running --format '{{.Names}}'");
            return dockerSuccess && !string.IsNullOrWhiteSpace(dockerOutput);
        }

        private async Task<bool> InstallPostgreSQLViaDocker()
        {
            if (!await IsCommandAvailable("docker"))
            {
                ConsoleHelper.MarkupLineWARNING("Docker is not installed.");

                if (!ConsoleHelper.PromptYesNo("Would you like to install Docker first?"))
                {
                    ShowManualInstallMessage("Docker", "https://docs.docker.com/engine/install/");
                    return false;
                }

                if (!await InstallDocker())
                {
                    return false;
                }

                ConsoleHelper.MarkupLineOK("Docker installed successfully! Now installing PostgreSQL...");
            }

            return await DockerInstaller.InstallPostgreSQLDocker();
        }

        public override string GetCheckingServiceMessage(string dbProviderName)
        {
            return $"Checking if your {dbProviderName} container is running...";
        }

        public override string GetServiceRunningMessage(string dbProviderName)
        {
            return $"Your {dbProviderName} container is running.";
        }

        public override string GetInstallPrompt(string dbProviderName)
        {
            return $"We couldn't find a running {dbProviderName} container. Would you like to install {dbProviderName} via Docker now?";
        }

        public override void ShowDeclinedInstallMessage(string dbProviderName, string manualInstallUrl)
        {
        }

        private async Task<bool> InstallDocker()
        {
            Console.WriteLine("\nAttempting to install Docker on Linux...");

            if (await IsCommandAvailable("apt-get"))
            {
                Console.WriteLine("Installing Docker via APT...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                await ProcessRunner.RunCommand("sudo", "apt-get update");
                await ProcessRunner.RunCommand("sudo", "apt-get install -y ca-certificates curl gnupg");
                await ProcessRunner.RunCommand("sudo", "install -m 0755 -d /etc/apt/keyrings");
                await ProcessRunner.RunCommand("/bin/bash", "-c \"curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg\"");
                await ProcessRunner.RunCommand("sudo", "chmod a+r /etc/apt/keyrings/docker.gpg");
                await ProcessRunner.RunCommand("/bin/bash", "-c \"echo 'deb [arch='$(dpkg --print-architecture)' signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu '$(. /etc/os-release && echo $VERSION_CODENAME)' stable' | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null\"");
                await ProcessRunner.RunCommand("sudo", "apt-get update");

                (bool successful, _) = await ProcessRunner.RunCommand("sudo", "apt-get install -y docker-ce docker-ce-cli containerd.io");
                if (successful)
                {
                    await ProcessRunner.RunCommand("sudo", "systemctl start docker");
                    await ProcessRunner.RunCommand("sudo", "systemctl enable docker");
                    return true;
                }
            }

            ShowManualInstallMessage("Docker", "https://docs.docker.com/engine/install/");
            return false;
        }
    }
}
