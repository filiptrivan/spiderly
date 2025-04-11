using CaseConverter;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using OfficeOpenXml.Utils;
using Spider.Shared;
using Spider.Shared.Extensions;
using Spider.Shared.Helpers;
using System.Data.SqlClient;
using System.Diagnostics;
using System.Reflection;

namespace Spider.ProjectInitializer
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
            Console.WriteLine("  template-type        Specify the template type. ('default' or 'loyalty').");
            Console.WriteLine("  primary-color        Specify the primary color for the application in hexadecimal format (e.g., #000000).");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  spider --help");
            Console.WriteLine("  spider init");
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

            Console.WriteLine("Template type (default/loyalty): ");
            string templateType = Console.ReadLine();
            if (templateType != "default" && templateType != "loyalty")
            {
                Console.WriteLine("Template type can only be default/loyalty.");
                return;
            }

            string currentPath = Environment.CurrentDirectory;

            string version = assembly.GetName().Version.ToString();

            if (templateType == "default")
                NetAndAngularStructureGenerator.Generate(currentPath, appName, version, IsFromNuGet(assembly), null);

            string infrastructurePath = Path.Combine(currentPath, @$"{appName}{_s_}{appName.ToKebabCase()}{_s_}API{_s_}{appName}.Infrastructure");
            string backendPath = Path.Combine(currentPath, @$"{appName}{_s_}{appName.ToKebabCase()}{_s_}API");
            string frontendPath = Path.Combine(currentPath, @$"{appName}{_s_}{appName.ToKebabCase()}{_s_}Angular");

            Console.WriteLine("\nAdding EF migration...");
            if (!await RunCommand("dotnet", @$"ef migrations add InitialCreate --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath))
            {
                return;
            }

            Console.WriteLine("\nUpdating database...");
            if (!await RunCommand("dotnet", @$"ef database update --project .{_s_}{appName}.Infrastructure.csproj --startup-project ..{_s_}{appName}.WebAPI{_s_}{appName}.WebAPI.csproj", infrastructurePath)) 
                return;

            Console.WriteLine("\nOpening Visual Studio...");
            OpenFile($".{_s_}{appName}.sln", null, backendPath);
            
            Console.WriteLine("\nNpm install...");
            if (!await RunCommand("npm", "install", frontendPath)) 
                return;

            Console.WriteLine("\nOpening Visual Studio Code...");
            OpenFile("code", ".", frontendPath);
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

        private static void OpenFile(string fileName, string arguments, string workingDirectory)
        {
            Process process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    UseShellExecute = true,
                    CreateNoWindow = false
                },
            };

            process.Start();
        }

        /// <summary>
        /// Returns true if the given assembly was loaded from the global NuGet cache (~/.nuget/packages),
        /// false if it came from your local build output (e.g. bin/Debug or bin/Release).
        /// </summary>
        private static bool IsFromNuGet(Assembly assembly)
        {
            if (assembly == null) 
                throw new ArgumentNullException(nameof(assembly));

            // If Location is empty, it's a dynamic or in-memory assembly; treat as local
            string location = assembly.Location;

            if (string.IsNullOrEmpty(location))
                return false;

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string nugetPackagesRoot = Path.Combine(userProfile, ".nuget", "packages") + Path.DirectorySeparatorChar;

            return location.StartsWith(nugetPackagesRoot, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }
    }
}