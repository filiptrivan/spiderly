using Spectre.Console;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Spiderly.CLI.Services
{
    [SupportedOSPlatform("windows")]
    internal class WindowsInstaller : BaseOSInstaller
    {
        protected override string ShellFileName => "cmd.exe";
        protected override string GetWhichCommand(string command) => $"/c where {command}";

        protected override async Task<(string command, string arguments)> GetPsqlCommandAsync(string sqlCommand)
        {
            string psqlPath = await FindPsqlPathAsync();
            if (string.IsNullOrEmpty(psqlPath))
            {
                return (string.Empty, string.Empty);
            }

            return (psqlPath, $"-U postgres -c \"{sqlCommand}\"");
        }

        public override async Task<bool> InstallPostgreSQL()
        {
            ConsoleHelper.MarkupLineLoading("Attempting to install PostgreSQL on Windows...");

            if (!await IsCommandAvailable("choco") && !await PromptAndInstallChocolatey())
            {
                ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/windows/");
                return false;
            }

            if (await InstallViaChocolatey("postgresql", "PostgreSQL"))
            {
                await StartPostgreSQLService();
                await ConfigurePostgreSQLAuthentication();
                return true;
            }

            ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/windows/");
            return false;
        }

        public override async Task<bool> InstallSqlServer()
        {
            ConsoleHelper.MarkupLineLoading("Attempting to install SQL Server on Windows...");

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

        private async Task<bool> InstallViaChocolatey(string packageName, string displayName)
        {
            if (!IsRunningAsAdmin())
            {
                ConsoleHelper.MarkupLineERROR("Administrator privileges required!");
                Console.WriteLine($"\nTo install {displayName} via Chocolatey, you need to run this application as Administrator.");
                Console.WriteLine("\nPlease restart your terminal/command prompt as Administrator and try again.");
                return false;
            }

            ConsoleHelper.MarkupLineLoading($"Chocolatey detected. Installing {displayName} via Chocolatey...");
            Console.WriteLine("This may take several minutes...");

            (bool installed, _) = await ProcessRunner.RunCommand("choco", $"install {packageName} -y");
            if (installed)
            {
                ConsoleHelper.MarkupLineOK($"{displayName} has been installed successfully.");
                return true;
            }

            return false;
        }

        private async Task<bool> PromptAndInstallChocolatey()
        {
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

        private async Task<bool> InstallChocolatey()
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

            ConsoleHelper.MarkupLineLoading("Installing Chocolatey package manager...");
            Console.WriteLine("This may take a few minutes...");

            string installScript = "[System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))";

            (bool installed, _) = await ProcessRunner.RunCommand("powershell", $"-NoProfile -ExecutionPolicy Bypass -Command \"{installScript}\"");

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
                ConsoleHelper.MarkupLineERROR("Failed to install Chocolatey.");
                AnsiConsole.MarkupLine("Please install Chocolatey manually and rerun 'spiderly init'.");
            }

            return installed;
        }

        private async Task StartPostgreSQLService()
        {
            ConsoleHelper.MarkupLineLoading("Starting PostgreSQL service...");

            (bool _, string output) = await ProcessRunner.RunCommand(
                "powershell",
                "-Command \"Get-Service | Where-Object {$_.Name -like '*postgresql*'} | Select-Object -ExpandProperty Name\""
            );

            if (!string.IsNullOrEmpty(output))
            {
                string[] serviceNames = output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                foreach (string serviceName in serviceNames)
                {
                    string trimmedName = serviceName.Trim();
                    if (!string.IsNullOrEmpty(trimmedName))
                    {
                        ConsoleHelper.MarkupLineOK($"Found PostgreSQL service: {trimmedName}");
                        await ProcessRunner.RunCommand("net", $"start \"{trimmedName}\"");
                        return;
                    }
                }
            }

            ConsoleHelper.MarkupLineWARNING("No PostgreSQL service found, attempting default name...");
            await ProcessRunner.RunCommand("net", "start postgresql");
        }

        private async Task<string> FindPsqlPathAsync()
        {
            List<string> basePaths =
            [
                @"C:\Program Files\PostgreSQL",
                @"C:\Program Files (x86)\PostgreSQL"
            ];

            foreach (string basePath in basePaths)
            {
                if (Directory.Exists(basePath))
                {
                    string[] versionDirs = Directory.GetDirectories(basePath);
                    foreach (string versionDir in versionDirs.OrderByDescending(d => d))
                    {
                        string psqlPath = Path.Combine(versionDir, "bin", "psql.exe");
                        if (File.Exists(psqlPath))
                        {
                            return psqlPath;
                        }
                    }
                }
            }

            (bool successful, string output) = await ProcessRunner.RunCommand(
                "cmd.exe",
                "/c where psql"
            );

            if (successful && !string.IsNullOrEmpty(output))
            {
                return output.Split('\n')[0].Trim();
            }

            return null;
        }

        public override async Task<bool> IsPostgreSQLServiceRunning()
        {
            (bool _, string output) = await ProcessRunner.RunCommand(
                "powershell",
                "-Command \"Get-Service | Where-Object {$_.Name -like '*postgresql*' -and $_.Status -eq 'Running'} | Select-Object -ExpandProperty Name\""
            );

            return !string.IsNullOrWhiteSpace(output);
        }

        public override async Task<bool> IsSqlServerServiceRunning()
        {
            (bool _, string output) = await ProcessRunner.RunCommand(
                "powershell",
                "-Command \"Get-Service | Where-Object {$_.Name -like '*MSSQL*' -and $_.Status -eq 'Running'} | Select-Object -ExpandProperty Name\""
            );

            if (!string.IsNullOrWhiteSpace(output))
                return true;

            (bool dockerSuccess, string dockerOutput) = await ProcessRunner.RunCommand("docker", "ps --filter name=sqlserver --filter status=running --format '{{.Names}}'");
            return dockerSuccess && !string.IsNullOrWhiteSpace(dockerOutput);
        }

        private static bool IsRunningAsAdmin()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}
