using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Configuration;
using Spider.Shared;
using Spider.Shared.Extensions;
using Spider.Shared.Helpers;
using System.Data.SqlClient;
using System.Reflection;

namespace Spider.ProjectInitializer
{
    internal static class Program
    {
        static void Main(string[] args)
        {
            if (args.HasArg("--help"))
            {
                ShowHelp();
                return;
            }

            if (args.HasArg("init"))
            {
                HandleProjectInit();
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

        private static void HandleProjectInit()
        {
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

            Console.WriteLine("Primary color (eg. #000000): ");
            string primaryColor = Console.ReadLine();
            if (string.IsNullOrEmpty(primaryColor) == false)
            {
                if (primaryColor.Count() != 7)
                {
                    Console.WriteLine("You need to pass 7 character primary color code.");
                    return;
                }
                if (primaryColor.First() != '#')
                {
                    Console.WriteLine("You need '#' as the first character of the primary color code.");
                    return;
                }
            }

            string currentPath = Environment.CurrentDirectory;

            if (templateType == "default")
                NetAndAngularStructureGenerator.Generate(currentPath, appName, primaryColor);
        }

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }
    }
}