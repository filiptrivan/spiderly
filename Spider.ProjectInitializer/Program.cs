using CaseConverter;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
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
                NetAndAngularStructureGenerator.Generate(currentPath, appName, version, null);

            string infrastructurePath = Path.Combine(currentPath, @$"{appName}\{appName.ToKebabCase()}\API\{appName}.Infrastructure");
            string backendPath = Path.Combine(currentPath, @$"{appName}\{appName.ToKebabCase()}\API\{appName}.WebAPI");
            string frontendPath = Path.Combine(currentPath, @$"{appName}\{appName.ToKebabCase()}\Angular");
            Console.WriteLine("\nAdding EF migration...");
            if (!await RunCommand("dotnet", "ef migrations add InitialCreate", backendPath)) 
                return;

            Console.WriteLine("\nUpdating database...");
            if (!await RunCommand("dotnet", "ef database update", backendPath)) 
                return;

            Console.WriteLine("\nStarting backend...");
            if (!await RunCommand("dotnet", "run", backendPath)) 
                return;

            Console.WriteLine("\nNpm install...");
            if (!await RunCommand("npm", "install", frontendPath)) 
                return;

            Console.WriteLine("\nStarting frontend...");
            if (!await RunCommand("npx", "ng serve", frontendPath)) 
                return;
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

            bool started = process.Start();
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