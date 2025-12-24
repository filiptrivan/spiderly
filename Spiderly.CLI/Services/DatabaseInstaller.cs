using Spiderly.Shared.Enums;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Services
{
    internal static class DatabaseInstaller
    {
        public static async Task<bool> InstallDatabase(DbProviderCodes dbProvider)
        {
            if (dbProvider == DbProviderCodes.SQLServer)
            {
                return await InstallSqlServer();
            }
            else
            {
                return await InstallPostgreSQL();
            }
        }

        private static async Task<bool> InstallPostgreSQL()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await InstallPostgreSQLWindows();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return await InstallPostgreSQLMac();
            }
            else
            {
                return await InstallPostgreSQLLinux();
            }
        }

        private static async Task<bool> InstallPostgreSQLWindows()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on Windows...");

            if (await IsCommandAvailable("choco"))
            {
                if (await InstallViaChocolatey("postgresql", "PostgreSQL"))
                {
                    await StartPostgreSQLServiceWindows();
                    return true;
                }
            }

            if (await IsCommandAvailable("scoop"))
            {
                Console.WriteLine("Scoop detected. Installing PostgreSQL via Scoop...");
                Console.WriteLine("This may take several minutes...");

                bool scoopInstalled = await RunCommand("scoop", "install postgresql", Environment.CurrentDirectory);
                if (scoopInstalled)
                {
                    Console.WriteLine("Starting PostgreSQL service...");
                    await RunCommand("pg_ctl", "start", Environment.CurrentDirectory);
                    return true;
                }
            }

            if (await PromptAndInstallChocolatey())
            {
                if (await InstallViaChocolatey("postgresql", "PostgreSQL"))
                {
                    await StartPostgreSQLServiceWindows();
                    return true;
                }
            }

            Console.WriteLine("\nPlease install PostgreSQL manually from: https://www.postgresql.org/download/windows/");
            return false;
        }

        private static async Task<bool> InstallPostgreSQLMac()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on macOS...");

            if (await IsCommandAvailable("brew"))
            {
                if (await InstallViaHomebrew("postgresql", "PostgreSQL"))
                {
                    await RunCommand("brew", "services start postgresql", Environment.CurrentDirectory);
                    return true;
                }
                return false;
            }

            if (await PromptAndInstallHomebrew())
            {
                if (await InstallViaHomebrew("postgresql", "PostgreSQL"))
                {
                    await RunCommand("brew", "services start postgresql", Environment.CurrentDirectory);
                    return true;
                }
            }

            Console.WriteLine("\nPlease install PostgreSQL manually from: https://www.postgresql.org/download/macosx/");
            return false;
        }

        private static async Task<bool> InstallPostgreSQLLinux()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on Linux...");

            if (await IsCommandAvailable("apt-get"))
            {
                Console.WriteLine("APT package manager detected. Installing PostgreSQL...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                if (await RunCommand("sudo", "apt-get update", Environment.CurrentDirectory))
                {
                    if (await RunCommand("sudo", "apt-get install -y postgresql postgresql-contrib", Environment.CurrentDirectory))
                    {
                        Console.WriteLine("Starting PostgreSQL service...");
                        await RunCommand("sudo", "systemctl start postgresql", Environment.CurrentDirectory);
                        await RunCommand("sudo", "systemctl enable postgresql", Environment.CurrentDirectory);
                        return true;
                    }
                }
            }
            else if (await IsCommandAvailable("yum"))
            {
                Console.WriteLine("YUM package manager detected. Installing PostgreSQL...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                if (await RunCommand("sudo", "yum install -y postgresql-server postgresql-contrib", Environment.CurrentDirectory))
                {
                    await RunCommand("sudo", "postgresql-setup --initdb", Environment.CurrentDirectory);
                    Console.WriteLine("Starting PostgreSQL service...");
                    await RunCommand("sudo", "systemctl start postgresql", Environment.CurrentDirectory);
                    await RunCommand("sudo", "systemctl enable postgresql", Environment.CurrentDirectory);
                    return true;
                }
            }
            else if (await IsCommandAvailable("dnf"))
            {
                Console.WriteLine("DNF package manager detected. Installing PostgreSQL...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                if (await RunCommand("sudo", "dnf install -y postgresql-server postgresql-contrib", Environment.CurrentDirectory))
                {
                    await RunCommand("sudo", "postgresql-setup --initdb", Environment.CurrentDirectory);
                    Console.WriteLine("Starting PostgreSQL service...");
                    await RunCommand("sudo", "systemctl start postgresql", Environment.CurrentDirectory);
                    await RunCommand("sudo", "systemctl enable postgresql", Environment.CurrentDirectory);
                    return true;
                }
            }
            else
            {
                Console.WriteLine("\n[INFO] No supported package manager (apt-get, yum, dnf) was detected.");

                if (await IsCommandAvailable("docker"))
                {
                    if (ConsoleHelper.PromptYesNo("Would you like to install PostgreSQL via Docker instead?"))
                    {
                        return await InstallPostgreSQLDocker();
                    }
                }
                else
                {
                    if (ConsoleHelper.PromptYesNo("Would you like to install Docker to run PostgreSQL in a container?"))
                    {
                        if (await InstallDockerLinux())
                        {
                            Console.WriteLine("\nDocker installed successfully! Now installing PostgreSQL...");
                            return await InstallPostgreSQLDocker();
                        }
                    }
                }
            }

            Console.WriteLine("\nPlease install PostgreSQL manually from: https://www.postgresql.org/download/linux/");
            return false;
        }

        private static async Task<bool> InstallSqlServer()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return await InstallSqlServerWindows();
            }
            else
            {
                return await InstallSqlServerDocker();
            }
        }

        private static async Task<bool> InstallSqlServerWindows()
        {
            Console.WriteLine("\nAttempting to install SQL Server on Windows...");

            if (await IsCommandAvailable("choco"))
            {
                if (await InstallViaChocolatey("sql-server-express", "SQL Server Express"))
                {
                    await Task.Delay(3000);
                    return true;
                }
            }

            if (await PromptAndInstallChocolatey())
            {
                if (await InstallViaChocolatey("sql-server-express", "SQL Server Express"))
                {
                    await Task.Delay(3000);
                    return true;
                }
            }

            Console.WriteLine("\nPlease install SQL Server Express manually from:");
            Console.WriteLine("https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
            return false;
        }

        private static async Task<bool> InstallSqlServerDocker()
        {
            Console.WriteLine("\nSQL Server requires Docker on macOS/Linux.");

            if (!await IsCommandAvailable("docker"))
            {
                Console.WriteLine("\n[INFO] Docker is not installed.");

                if (ConsoleHelper.PromptYesNo("Would you like to install Docker to run SQL Server in a container?"))
                {
                    bool dockerInstalled = false;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        dockerInstalled = await InstallDockerLinux();
                    }
                    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    {
                        dockerInstalled = await InstallDockerMac();
                    }

                    if (dockerInstalled)
                    {
                        Console.WriteLine("\nDocker installed successfully! Now installing SQL Server...");
                        return await InstallSqlServerDockerContainer();
                    }
                }
                else
                {
                    Console.WriteLine("Please install Docker manually from: https://www.docker.com/get-started");
                }

                return false;
            }

            return await InstallSqlServerDockerContainer();
        }

        private static async Task<bool> InstallSqlServerDockerContainer()
        {
            Console.WriteLine("Docker detected. Installing SQL Server via Docker...");
            Console.WriteLine("This may take several minutes...");

            string dockerCommand = "run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourStrong@Passw0rd\" -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest";

            if (await RunCommand("docker", dockerCommand, Environment.CurrentDirectory))
            {
                Console.WriteLine("\nSQL Server container has been started.");
                Console.WriteLine("Connection details:");
                Console.WriteLine("  Server: localhost,1433");
                Console.WriteLine("  Username: sa");
                Console.WriteLine("  Password: YourStrong@Passw0rd");
                return true;
            }

            return false;
        }

        private static async Task<bool> IsCommandAvailable(string command)
        {
            try
            {
                bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
                string fileName = isWin ? "cmd.exe" : "/bin/bash";
                string arguments = isWin ? $"/c where {command}" : $"-c \"which {command}\"";

                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = fileName,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();

                return process.ExitCode == 0;
            }
            catch
            {
                return false;
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

        private static async Task<bool> InstallViaChocolatey(string packageName, string displayName)
        {
            Console.WriteLine($"Chocolatey detected. Installing {displayName} via Chocolatey...");
            Console.WriteLine("This may take several minutes...");

            bool installed = await RunCommand("choco", $"install {packageName} -y", Environment.CurrentDirectory);
            if (installed)
            {
                Console.WriteLine($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private static async Task<bool> PromptAndInstallChocolatey()
        {
            Console.WriteLine("\n[INFO] Chocolatey package manager was not found.");

            if (ConsoleHelper.PromptYesNo("Would you like to install Chocolatey package manager?"))
            {
                if (await InstallChocolatey())
                {
                    Console.WriteLine("\nChocolatey installed successfully!");
                    return true;
                }
                else
                {
                    Console.WriteLine("\n[ERROR] Failed to install Chocolatey.");
                    return false;
                }
            }

            return false;
        }

        private static async Task<bool> InstallChocolatey()
        {
            Console.WriteLine("\nInstalling Chocolatey package manager...");
            Console.WriteLine("This requires administrator privileges and may take a few minutes...");

            string installScript = "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))";

            bool installed = await RunCommand("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{installScript}\"", Environment.CurrentDirectory);

            if (installed)
            {
                Environment.SetEnvironmentVariable("ChocolateyInstall", @"C:\ProgramData\chocolatey", EnvironmentVariableTarget.Machine);
                string path = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.Machine);
                if (!path.Contains(@"C:\ProgramData\chocolatey\bin"))
                {
                    Environment.SetEnvironmentVariable("PATH", path + @";C:\ProgramData\chocolatey\bin", EnvironmentVariableTarget.Machine);
                }
            }

            return installed;
        }

        private static async Task<bool> InstallViaHomebrew(string packageName, string displayName)
        {
            Console.WriteLine($"Homebrew detected. Installing {displayName}...");
            Console.WriteLine("This may take several minutes...");

            bool installed = await RunCommand("brew", $"install {packageName}", Environment.CurrentDirectory);
            if (installed)
            {
                Console.WriteLine($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private static async Task<bool> PromptAndInstallHomebrew()
        {
            Console.WriteLine("\n[INFO] Homebrew is not installed.");

            if (ConsoleHelper.PromptYesNo("Would you like to install Homebrew package manager?"))
            {
                if (await InstallHomebrew())
                {
                    Console.WriteLine("\nHomebrew installed successfully!");
                    return true;
                }
                else
                {
                    Console.WriteLine("\n[ERROR] Failed to install Homebrew.");
                    return false;
                }
            }

            return false;
        }

        private static async Task<bool> InstallHomebrew()
        {
            Console.WriteLine("\nInstalling Homebrew package manager...");
            Console.WriteLine("This may take several minutes...");

            string installScript = "/bin/bash -c \"$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)\"";

            return await RunCommand("/bin/bash", $"-c \"{installScript}\"", Environment.CurrentDirectory);
        }

        private static async Task StartPostgreSQLServiceWindows()
        {
            Console.WriteLine("Starting PostgreSQL service...");

            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = "/c sc query | findstr postgresql",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (!string.IsNullOrEmpty(output))
            {
                string[] lines = output.Split('\n');
                foreach (string line in lines)
                {
                    if (line.Trim().StartsWith("SERVICE_NAME:"))
                    {
                        string serviceName = line.Replace("SERVICE_NAME:", "").Trim();
                        await RunCommand("net", $"start {serviceName}", Environment.CurrentDirectory);
                        return;
                    }
                }
            }

            await RunCommand("net", "start postgresql", Environment.CurrentDirectory);
        }

        private static async Task<bool> InstallPostgreSQLDocker()
        {
            Console.WriteLine("\nInstalling PostgreSQL via Docker...");
            Console.WriteLine("This may take several minutes...");

            string dockerCommand = "run --name postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:latest";

            if (await RunCommand("docker", dockerCommand, Environment.CurrentDirectory))
            {
                Console.WriteLine("\nPostgreSQL container has been started.");
                Console.WriteLine("Connection details:");
                Console.WriteLine("  Host: localhost");
                Console.WriteLine("  Port: 5432");
                Console.WriteLine("  Username: postgres");
                Console.WriteLine("  Password: postgres");
                return true;
            }

            return false;
        }

        private static async Task<bool> InstallDockerMac()
        {
            Console.WriteLine("\nAttempting to install Docker on macOS...");

            if (await IsCommandAvailable("brew"))
            {
                if (await InstallViaHomebrew("--cask docker", "Docker Desktop"))
                {
                    Console.WriteLine("\nDocker Desktop has been installed.");
                    Console.WriteLine("Please start Docker Desktop from your Applications folder before continuing.");
                    return true;
                }
                return false;
            }

            if (await PromptAndInstallHomebrew())
            {
                if (await InstallViaHomebrew("--cask docker", "Docker Desktop"))
                {
                    Console.WriteLine("\nDocker Desktop has been installed.");
                    Console.WriteLine("Please start Docker Desktop from your Applications folder before continuing.");
                    return true;
                }
            }

            Console.WriteLine("\n[ERROR] Failed to install Docker.");
            Console.WriteLine("Please install Docker manually from: https://www.docker.com/get-started");
            return false;
        }

        private static async Task<bool> InstallDockerLinux()
        {
            Console.WriteLine("\nAttempting to install Docker on Linux...");

            if (await IsCommandAvailable("apt-get"))
            {
                Console.WriteLine("Installing Docker via APT...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                await RunCommand("sudo", "apt-get update", Environment.CurrentDirectory);
                await RunCommand("sudo", "apt-get install -y ca-certificates curl gnupg", Environment.CurrentDirectory);
                await RunCommand("sudo", "install -m 0755 -d /etc/apt/keyrings", Environment.CurrentDirectory);
                await RunCommand("/bin/bash", "-c \"curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo gpg --dearmor -o /etc/apt/keyrings/docker.gpg\"", Environment.CurrentDirectory);
                await RunCommand("sudo", "chmod a+r /etc/apt/keyrings/docker.gpg", Environment.CurrentDirectory);
                await RunCommand("/bin/bash", "-c \"echo 'deb [arch='$(dpkg --print-architecture)' signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu '$(. /etc/os-release && echo $VERSION_CODENAME)' stable' | sudo tee /etc/apt/sources.list.d/docker.list > /dev/null\"", Environment.CurrentDirectory);
                await RunCommand("sudo", "apt-get update", Environment.CurrentDirectory);

                if (await RunCommand("sudo", "apt-get install -y docker-ce docker-ce-cli containerd.io", Environment.CurrentDirectory))
                {
                    await RunCommand("sudo", "systemctl start docker", Environment.CurrentDirectory);
                    await RunCommand("sudo", "systemctl enable docker", Environment.CurrentDirectory);
                    return true;
                }
            }
            else if (await IsCommandAvailable("yum") || await IsCommandAvailable("dnf"))
            {
                string pkgManager = await IsCommandAvailable("dnf") ? "dnf" : "yum";
                Console.WriteLine($"Installing Docker via {pkgManager.ToUpper()}...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                await RunCommand("sudo", $"{pkgManager} install -y yum-utils", Environment.CurrentDirectory);
                await RunCommand("sudo", "yum-config-manager --add-repo https://download.docker.com/linux/centos/docker-ce.repo", Environment.CurrentDirectory);

                if (await RunCommand("sudo", $"{pkgManager} install -y docker-ce docker-ce-cli containerd.io", Environment.CurrentDirectory))
                {
                    await RunCommand("sudo", "systemctl start docker", Environment.CurrentDirectory);
                    await RunCommand("sudo", "systemctl enable docker", Environment.CurrentDirectory);
                    return true;
                }
            }

            Console.WriteLine("\n[ERROR] Failed to install Docker.");
            Console.WriteLine("Please install Docker manually from: https://docs.docker.com/engine/install/");
            return false;
        }
    }
}
