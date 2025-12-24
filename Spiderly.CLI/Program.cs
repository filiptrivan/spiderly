using Spiderly.CLI.Commands;
using System.Reflection;

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
        private static async Task Main(string[] args)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string fullVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            string version = fullVersion?.Split('+')[0]; // If we don't split, it will return the full version with the commit hash, which is not needed for the init command.

            if (args.HasArg("--help") || args.HasArg("-help") || args.HasArg("help"))
            {
                ShowHelp();
                return;
            }
            else if (args.HasArg("init"))
            {
                bool hasTopMenu = args.HasArg("--top-menu");
                bool isRunningFromNuget = !args.HasArg("--dev");

                await InitCommand.Execute(hasTopMenu, isRunningFromNuget, version);
                return;
            }
            else if (args.HasArg("add-new-page"))
            {
                bool shouldGenerateDataView = args.HasArg("--data-view");

                await AddNewPageCommand.Execute(shouldGenerateDataView);
                return;
            }
            else if (args.Length == 0)
            {
                Console.WriteLine($$"""
           ____        _     _           _
 ||  ||   / ___| _ __ (_) __| | ___ _ __| |_   _
 \\()//   \___ \| '_ \| |/ _` |/ _ \ '__| | | | |
//(__)\\   ___) | |_) | | (_| |  __/ |  | | |_| |
||    ||  |____/| .__/|_|\__,_|\___|_|  |_|\__, |
                |_|                        |___/

Spiderly.CLI v{{version}}
-------------------------------------------------
Type 'spiderly help' to see a list of available commands.
""");
            }
            else
            {
                Console.WriteLine("Unrecognized command. Type 'spiderly help' to see a list of available commands.");
            }
        }

        private static void ShowHelp()
        {
            Console.WriteLine("Usage: [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  help                 Display this help message.");
            Console.WriteLine("  init                 Initialize a new project.");
            Console.WriteLine("  add-new-page         Generates starter files to support CRUD operations for a new entity.");
            Console.WriteLine();
            Console.WriteLine("Options for init:");
            Console.WriteLine("  --top-menu           Use a top menu layout instead of the default side menu layout.");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  spiderly help");
            Console.WriteLine("  spiderly init");
            Console.WriteLine("  spiderly add-new-page");
        }

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }
    }
}
