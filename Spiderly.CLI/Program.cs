using CaseConverter;
using Microsoft.Extensions.Configuration;
using Spiderly.Shared.Extensions;
using Spiderly.Shared.Helpers;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Spiderly.CLI
{
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

            Console.WriteLine("App name without spaces: ");
            string appName = Console.ReadLine();
            if (string.IsNullOrEmpty(appName))
            {
                Console.WriteLine("Your app name can't be null or empty.");
                return;
            }
            if (appName.HasSpaces())
            {
                Console.WriteLine("Your app name can't have spaces.");
                return;
            }

            string currentPath = Environment.CurrentDirectory;

            string version = assembly.GetName().Version.ToString();

            bool hasErrors = NetAndAngularStructureGenerator.Generate(currentPath, appName, version, isFromNuget: true, null);

            string infrastructurePath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}API{_s_}{appName}.Infrastructure");
            string backendPath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}API");
            string frontendPath = Path.Combine(currentPath, @$"{appName.ToKebabCase()}{_s_}Angular");

            Console.WriteLine("\nAdding EF migration...");
            if (!await RunCommand("dotnet", @$"ef migrations add InitialCreate --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\nFailed to add EF migration.");
                hasErrors = true;
            }

            Console.WriteLine("\nUpdating database...");
            if (!await RunCommand("dotnet", @$"ef database update --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                Console.WriteLine("\nFailed to update database.");
                hasErrors = true;
            }

            Console.WriteLine("\nNpm install...");
            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string npmCmd = isWin ? "cmd.exe" : "/bin/bash";
            string npmArgs = isWin ? "/c npm install" : "-c \"npm install\"";
            if (!await RunCommand(npmCmd, npmArgs, frontendPath))
            {
                Console.WriteLine("\nFailed to npm install.");
                hasErrors = true;
            }

            if (hasErrors)
            {
                Console.WriteLine("You had some errors. Please solve them, then run the same command (`spiderly init`) again with the same app name to continue.");
            }
            else
            {
                Console.WriteLine("Basic Spiderly app structure created!");
                Console.WriteLine("Open the frontend and backend projects in your preferred IDEs.");
                Console.WriteLine("Start the backend using the 'dotnet run' command.");
                Console.WriteLine("Start the frontend using the 'npm start' command.");
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