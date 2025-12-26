using System.Runtime.Versioning;
using System.Security.Principal;
using Spectre.Console;
using Spiderly.CLI.Services;

[SupportedOSPlatform("windows")]
public static class WindowsConsoleHelper
{
    public static async Task<bool> InstallViaChocolatey(string packageName, string displayName)
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

    public static async Task<bool> PromptAndInstallChocolatey()
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

    public static async Task<bool> InstallChocolatey()
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

    public static bool IsRunningAsAdmin()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

}