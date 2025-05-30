using CaseConverter;
using Microsoft.Extensions.Configuration;
using Spiderly.Shared.Exceptions;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using System;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spiderly.CLI
{
    /// <summary>
    /// The main entry point for the Spiderly command-line interface (CLI) tool.
    /// This class handles parsing command-line arguments, displaying help information,
    /// and executing commands such as initializing a new Spiderly project structure
    /// with a .NET backend and an Angular frontend.
    /// </summary>
    internal static class Program
    {
        private static readonly char _s_ = Path.DirectorySeparatorChar;

        static async Task Main(string[] args)
        {
            if (args.HasArg("--help"))
            {
                ShowHelp();
                return;
            }

            if (args.HasArg("init"))
            {
                await HandleProjectInit();
            }

            IConfiguration config = new ConfigurationBuilder()
                .AddCommandLine(args)
                .Build();

            //string initType = config["init"];
            //string primaryColor = config["primary-color"];
            //string appName = config["app-name"];
            //string currentPath = Environment.CurrentDirectory;
        }


        private static void ShowHelp()
        {
            Console.WriteLine("Usage: [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  --help               Display this help message.");
            Console.WriteLine("  init                 Initialize a new project.");
            Console.WriteLine();
            Console.WriteLine("Options for init:");
            Console.WriteLine("  app-name             Specify the name of the application. (No spaces allowed.)");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  spiderly --help");
            Console.WriteLine("  spiderly init");
        }

        private static async Task HandleProjectInit()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            string appName;

            while (true)
            {
                Console.Write("App name without spaces (e.g., YourAppName): ");
                appName = Console.ReadLine();

                if (string.IsNullOrEmpty(appName))
                {
                    Console.WriteLine("Your app name can't be null or empty.");
                    continue;
                }

                if (appName.Contains(" "))
                {
                    Console.WriteLine("Your app name can't have spaces.");
                    continue;
                }

                break;
            }

            string currentPath = Environment.CurrentDirectory;

            string version = assembly.GetName().Version.ToString();

            bool hasNetAndAngularInitErrors = false;
            bool hasEfMigrationErrors = false;
            bool hasDatabaseUpdateErrors = false;
            bool hasNpmInstallErrors = false;

            Console.WriteLine("\nGenerating files for the app...");
            try
            {
                NetAndAngularStructureGenerator.Generate(currentPath, appName, version, isFromNuget: true, null);
                Console.WriteLine("Finished generating files for the app.");
            }
            catch (Exception ex)
            {
                if (ex is BusinessException)
                {
                    Console.WriteLine($"[ERROR] Error occurred:\n{ex.Message}");
                }
                else
                {
                    Console.WriteLine($"[ERROR] Error occurred:\n{ex}");
                }

                hasNetAndAngularInitErrors = true;
            }

            string infrastructurePath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}API{_s_}{appName}.Infrastructure");
            string frontendPath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}Angular");

            Console.WriteLine("\nGenerating the database migration...");
            if (!await RunCommand("dotnet", @$"ef migrations add InitialCreate --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\n[ERROR] Failed to generate the database migration.");
                hasEfMigrationErrors = true;
            }

            Console.WriteLine("\nUpdating the database...");
            if (!await RunCommand("dotnet", @$"ef database update --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\n[ERROR] Failed to update the database.");
                hasDatabaseUpdateErrors = true;
            }

            Console.WriteLine("\nInstalling frontend packages...");
            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            if (!await RunCommand(npmCmd, npmArgs, frontendPath))
            {
                Console.WriteLine("\n[ERROR] Failed to install frontend packages.");
                hasNpmInstallErrors = true;
            }

            if (hasNetAndAngularInitErrors || hasEfMigrationErrors || hasDatabaseUpdateErrors || hasNpmInstallErrors)
            {
                if (hasNetAndAngularInitErrors)
                {
                    Console.WriteLine("\nError occurred while generating files for the app.");
                }
                else if (hasEfMigrationErrors)
                {
                    Console.WriteLine("\nError occurred while generating database migration.");
                }
                else if (hasDatabaseUpdateErrors)
                {
                    Console.WriteLine("\nError occurred while initializing the database.");
                }
                else if (hasNpmInstallErrors)
                {
                    Console.WriteLine("\nError occurred while installing frontend packages.");
                }

                Console.WriteLine("\nPlease fix the errors, then rerun the 'spiderly init' command using the same app name and location.");
            }
            else
            {
                Console.WriteLine("\nApp initialized successfully, continue with the Step 4 from the getting started guide!");
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

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }
    }
}