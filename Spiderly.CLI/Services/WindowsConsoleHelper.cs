using System.Runtime.Versioning;
using System.Security.Principal;
using System.Diagnostics;
using Spectre.Console;
using Spiderly.CLI.Services;

[SupportedOSPlatform("windows")]
public static class WindowsConsoleHelper
{
    public static async Task<bool> InstallViaChocolatey(string packageName, string displayName, string installParams = null)
    {
        if (!IsRunningAsAdmin())
        {
            ConsoleHelper.MarkupLineWARNING("Administrator privileges required for Chocolatey installation.");

            if (ConsoleHelper.PromptYesNo("Would you like to restart the application as Administrator?"))
            {
                if (RestartAsAdmin())
                {
                    Environment.Exit(0);
                }
            }

            AnsiConsole.WriteLine();
            ConsoleHelper.MarkupLineERROR("Cannot proceed without administrator privileges.");
            Console.WriteLine("Please restart your terminal/command prompt as Administrator and try again.");
            return false;
        }

        ConsoleHelper.MarkupLineLoading($"Chocolatey detected. Installing {displayName} via Chocolatey...");
        Console.WriteLine("This may take several minutes...");

        string command = string.IsNullOrEmpty(installParams)
            ? $"install {packageName} -y"
            : $"install {packageName} -y --params '{installParams}'";

        (bool installed, _) = await ProcessRunner.RunCommand("choco", command);
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
            ConsoleHelper.MarkupLineWARNING("Administrator privileges required for Chocolatey installation.");

            if (ConsoleHelper.PromptYesNo("Would you like to restart the application as Administrator?"))
            {
                if (RestartAsAdmin())
                {
                    Environment.Exit(0);
                }
            }

            AnsiConsole.WriteLine();
            ConsoleHelper.MarkupLineERROR("Cannot proceed without administrator privileges.");
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

    public static bool RestartAsAdmin()
    {
        try
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath,
                Verb = "runas",
                Arguments = string.Join(" ", Environment.GetCommandLineArgs().Skip(1))
            };

            Process.Start(processInfo);
            return true;
        }
        catch (Exception ex)
        {
            ConsoleHelper.MarkupLineERROR($"Failed to restart as administrator: {ex.Message}");
            return false;
        }
    }

}