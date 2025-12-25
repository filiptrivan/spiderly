using Spectre.Console;
using Spiderly.Shared.Enums;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Services
{
    internal static class DatabaseInstaller
    {
        private static IOSInstaller GetOSInstaller()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new WindowsInstaller();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new MacInstaller();
            }
            else
            {
                return new LinuxInstaller();
            }
        }

        public static async Task<bool> IsPostgreSQLServiceRunning()
        {
            IOSInstaller installer = GetOSInstaller();
            return await installer.IsPostgreSQLServiceRunning();
        }

        public static async Task<bool> IsSqlServerServiceRunning()
        {
            IOSInstaller installer = GetOSInstaller();
            return await installer.IsSqlServerServiceRunning();
        }

        public static async Task<bool> InstallSqlServer()
        {
            IOSInstaller installer = GetOSInstaller();
            bool isServiceRunning = await installer.IsSqlServerServiceRunning();

            if (isServiceRunning)
            {
                ConsoleHelper.MarkupLineOK("SQL Server service is already running.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("If you can't connect, please check your SQL Server credentials.");
                AnsiConsole.MarkupLine("[dim](You can configure it later and run migration and update scripts)[/]");
                return false;
            }

            return await installer.InstallSqlServer();
        }

        public static async Task<bool> InstallPostgreSQL()
        {
            IOSInstaller installer = GetOSInstaller();
            bool isServiceRunning = await installer.IsPostgreSQLServiceRunning();

            if (isServiceRunning)
            {
                ConsoleHelper.MarkupLineOK("PostgreSQL service is already running.");
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("If you can't connect, please enter your PostgreSQL password.");
                AnsiConsole.MarkupLine("[dim](If you don't remember it, you can configure it later and run migration and update scripts)[/]");
                return false;
            }

            return await installer.InstallPostgreSQL();
        }

        public static async Task<bool> InstallDatabase(DbProviderCodes dbProvider)
        {
            if (dbProvider == DbProviderCodes.SQLServer)
            {
                return await InstallSqlServer();
            }

            return await InstallPostgreSQL();
        }
    }
}
