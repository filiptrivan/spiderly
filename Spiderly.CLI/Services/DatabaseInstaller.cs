using Spectre.Console;
using Spiderly.Shared.Enums;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;

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

            if (!await IsCommandAvailable("choco") && !await PromptAndInstallChocolatey())
            {
                ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/windows/");
                return false;
            }

            if (await InstallViaChocolatey("postgresql", "PostgreSQL"))
            {
                await StartPostgreSQLServiceWindows();
                return true;
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/windows/");
            return false;
        }

        private static async Task<bool> InstallPostgreSQLMac()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on macOS...");

            if (!await IsCommandAvailable("brew") && !await PromptAndInstallHomebrew())
            {
                ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/macosx/");
                return false;
            }

            if (await InstallViaHomebrew("postgresql", "PostgreSQL"))
            {
                await RunCommand("brew", "services start postgresql", Environment.CurrentDirectory);
                return true;
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/macosx/");
            return false;
        }

        private static async Task<bool> InstallPostgreSQLLinux()
        {
            Console.WriteLine("\nAttempting to install PostgreSQL on Linux...");

            if (await IsCommandAvailable("apt-get"))
            {
                Console.WriteLine("APT package manager detected. Installing PostgreSQL...");
                Console.WriteLine("This may take several minutes and may require sudo password...");

                if (await RunCommand("sudo", "apt-get update", Environment.CurrentDirectory) &&
                    await RunCommand("sudo", "apt-get install -y postgresql postgresql-contrib", Environment.CurrentDirectory))
                {
                    Console.WriteLine("Starting PostgreSQL service...");
                    await RunCommand("sudo", "systemctl start postgresql", Environment.CurrentDirectory);
                    await RunCommand("sudo", "systemctl enable postgresql", Environment.CurrentDirectory);
                    return true;
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
                ConsoleHelper.MarkupLineWARNING("No supported package manager (apt-get, yum, dnf) was detected.");

                if (await IsCommandAvailable("docker"))
                {
                    if (ConsoleHelper.PromptYesNo("Would you like to install PostgreSQL via Docker instead?"))
                    {
                        return await InstallPostgreSQLDocker();
                    }
                }
                else if (ConsoleHelper.PromptYesNo("Would you like to install Docker to run PostgreSQL in a container?"))
                {
                    if (await InstallDockerLinux())
                    {
                        ConsoleHelper.MarkupLineOK("Docker installed successfully! Now installing PostgreSQL...");
                        return await InstallPostgreSQLDocker();
                    }
                }
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/linux/");
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

            if (!await IsCommandAvailable("choco") && !await PromptAndInstallChocolatey())
            {
                ShowManualInstallMessage("SQL Server Express", "https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
                return false;
            }

            if (await InstallViaChocolatey("sql-server-express", "SQL Server Express"))
            {
                await Task.Delay(3000);
                return true;
            }

            ShowManualInstallMessage("SQL Server Express", "https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
            return false;
        }

        private static async Task<bool> InstallSqlServerDocker()
        {
            Console.WriteLine("\nSQL Server requires Docker on macOS/Linux.");

            if (!await IsCommandAvailable("docker"))
            {
                ConsoleHelper.MarkupLineWARNING("Docker is not installed.");

                if (!ConsoleHelper.PromptYesNo("Would you like to install Docker to run SQL Server in a container?"))
                {
                    ShowManualInstallMessage("Docker", "https://www.docker.com/get-started");
                    return false;
                }

                bool dockerInstalled = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? await InstallDockerLinux()
                    : await InstallDockerMac();

                if (dockerInstalled)
                {
                    ConsoleHelper.MarkupLineOK("Docker installed successfully! Now installing SQL Server...");
                    return await InstallSqlServerDockerContainer();
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
            if (!IsRunningAsAdmin())
            {
                ConsoleHelper.MarkupLineERROR("Administrator privileges required!");
                Console.WriteLine($"\nTo install {displayName} via Chocolatey, you need to run this application as Administrator.");
                Console.WriteLine("\nPlease restart your terminal/command prompt as Administrator and try again.");
                return false;
            }

            Console.WriteLine($"Chocolatey detected. Installing {displayName} via Chocolatey...");
            Console.WriteLine("This may take several minutes...");

            bool installed = await RunCommand("choco", $"install {packageName} -y", Environment.CurrentDirectory);
            if (installed)
            {
                ConsoleHelper.MarkupLineOK($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private static async Task<bool> PromptAndInstallChocolatey()
        {
            AnsiConsole.WriteLine();
            ConsoleHelper.MarkupLineWARNING("Chocolatey package manager was not found.");

            if (ConsoleHelper.PromptYesNo("Would you like to install Chocolatey package manager?"))
            {
                if (await InstallChocolatey())
                {
                    ConsoleHelper.MarkupLineOK("Chocolatey installed successfully!");
                    return true;
                }
                return false;
            }

            return false;
        }

        private static async Task<bool> InstallChocolatey()
        {
            if (!IsRunningAsAdmin())
            {
                AnsiConsole.WriteLine();
                ConsoleHelper.MarkupLineERROR("Administrator privileges required to install Chocolatey.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Please restart your terminal as Administrator:");
                AnsiConsole.MarkupLine("  [dim]Right-click on your terminal/command prompt and select 'Run as Administrator'[/]");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("Or install Chocolatey manually:");
                AnsiConsole.MarkupLine("  [dim]1. Open PowerShell as Administrator[/]");
                AnsiConsole.MarkupLine("  [dim]2. Run: Set-ExecutionPolicy Bypass -Scope Process -Force[/]");
                AnsiConsole.MarkupLine("  [dim]3. Run: iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))[/]");
                return false;
            }

            Console.WriteLine();
            Console.WriteLine("Installing Chocolatey package manager...");
            Console.WriteLine("This may take a few minutes...");

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
            else
            {
                AnsiConsole.WriteLine();
                ConsoleHelper.MarkupLineERROR("Failed to install Chocolatey.");
                AnsiConsole.MarkupLine("Please install Chocolatey manually and rerun 'spiderly init'.");
            }

            return installed;
        }

        private static async Task<bool> InstallViaHomebrew(string packageName, string displayName)
        {
            Console.WriteLine($"\nHomebrew detected. Installing {displayName}...");
            Console.WriteLine("This may take several minutes...");

            bool installed = await RunCommand("brew", $"install {packageName}", Environment.CurrentDirectory);
            if (installed)
            {
                ConsoleHelper.MarkupLineOK($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private static async Task<bool> PromptAndInstallHomebrew()
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
                ConsoleHelper.MarkupLineOK("PostgreSQL container has been started.");
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

            ShowManualInstallMessage("Docker", "https://docs.docker.com/engine/install/");
            return false;
        }

        private static bool IsRunningAsAdmin()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    WindowsPrincipal principal = new WindowsPrincipal(identity);
                    return principal.IsInRole(WindowsBuiltInRole.Administrator);
                }
            }

            return true;
        }

        private static void ShowManualInstallMessage(string displayName, string url)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"You can also install {displayName} manually:");
            AnsiConsole.MarkupLine($"  [link]{url}[/]");
        }
    }
}
