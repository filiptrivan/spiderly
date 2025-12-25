using Spectre.Console;

namespace Spiderly.CLI.Services
{
    internal class LinuxInstaller : BaseOSInstaller
    {
        protected override string ShellFileName => "/bin/bash";
        protected override string GetWhichCommand(string command) => $"-c \"which {command}\"";

        protected override async Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand)
        {
            return ("sudo", $"-u postgres psql -c \"{sqlCommand}\"");
        }

        public override async Task<bool> InstallPostgreSQL()
        {
            ConsoleHelper.MarkupLineLoading("Attempting to install PostgreSQL on Linux...");

            if (await IsCommandAvailable("apt-get"))
            {
                ConsoleHelper.MarkupLineLoading("APT package manager detected. Installing PostgreSQL...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                (bool updateSuccess, _) = await ProcessRunner.RunCommand("sudo", "apt-get update");
                (bool installSuccess, _) = await ProcessRunner.RunCommand("sudo", "apt-get install -y postgresql postgresql-contrib");

                if (updateSuccess && installSuccess)
                {
                    ConsoleHelper.MarkupLineLoading("Starting PostgreSQL service...");
                    await ProcessRunner.RunCommand("sudo", "systemctl start postgresql");
                    await ProcessRunner.RunCommand("sudo", "systemctl enable postgresql");
                    await ConfigurePostgreSQLAuthentication();
                    return true;
                }
            }
            else
            {
                ConsoleHelper.MarkupLineWARNING("APT package manager was not detected.");

                if (await IsCommandAvailable("docker"))
                {
                    if (ConsoleHelper.PromptYesNo("Would you like to install PostgreSQL via Docker instead?"))
                    {
                        return await DockerInstaller.InstallPostgreSQLDocker();
                    }
                }
                else if (ConsoleHelper.PromptYesNo("Would you like to install Docker to run PostgreSQL in a container?"))
                {
                    if (await InstallDocker())
                    {
                        ConsoleHelper.MarkupLineOK("Docker installed successfully! Now installing PostgreSQL...");
                        return await DockerInstaller.InstallPostgreSQLDocker();
                    }
                }
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/linux/");
            return false;
        }

        public override async Task<bool> InstallSqlServer()
        {
            ConsoleHelper.MarkupLineLoading("SQL Server requires Docker on Linux...");
            return await DockerInstaller.InstallSqlServerDocker(this, InstallDocker);
        }

        public override async Task<bool> IsPostgreSQLServiceRunning()
        {
            (bool success, string output) = await ProcessRunner.RunCommand("sudo", "systemctl is-active postgresql");
            if (success && output.Trim() == "active")
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
