using Spectre.Console;
using Spiderly.Shared.Enums;
using Spiderly.Shared.Helpers;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Spiderly.CLI.Services.Database.OS
{
    [SupportedOSPlatform("windows")]
    internal class WindowsInstaller : BaseOSInstaller
    {
        protected override string ShellFileName => "cmd.exe";
        protected override string GetWhichCommand(string command) => $"/c where {command}";

        public WindowsInstaller(DbProviderCodes dbProvider) : base(dbProvider)
        {
        }

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
            ConsoleHelper.MarkupLineLoading("Attempting to install PostgreSQL...");

            if (!await IsCommandAvailable("choco") && !await WindowsConsoleHelper.PromptAndInstallChocolatey())
            {
                ShowManualInstallMessage("PostgreSQL", "https://www.postgresql.org/download/windows/");
                return false;
            }

            if (await WindowsConsoleHelper.InstallViaChocolatey("postgresql", "PostgreSQL"))
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
            ConsoleHelper.MarkupLineLoading("Attempting to install SQL Server...");

            if (!await IsCommandAvailable("choco") && !await WindowsConsoleHelper.PromptAndInstallChocolatey())
            {
                ShowManualInstallMessage("SQL Server Express", "https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
                return false;
            }

            if (await WindowsConsoleHelper.InstallViaChocolatey("sql-server-express", "SQL Server Express"))
            {
                await Task.Delay(3000);
                return true;
            }

            ShowManualInstallMessage("SQL Server Express", "https://www.microsoft.com/en-us/sql-server/sql-server-downloads");
            return false;
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

    }
}
