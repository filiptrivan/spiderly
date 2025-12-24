using Spectre.Console;
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
                string appName = args.GetArgValue("--name");
                string dbProvider = args.GetArgValue("--db");

                await InitCommand.Execute(hasTopMenu, isRunningFromNuget, version, appName, dbProvider);
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
                AnsiConsole.WriteLine($$"""
           ____        _     _           _
 ||  ||   / ___| _ __ (_) __| | ___ _ __| |_   _
 \\()//   \___ \| '_ \| |/ _` |/ _ \ '__| | | | |
//(__)\\   ___) | |_) | | (_| |  __/ |  | | |_| |
||    ||  |____/| .__/|_|\__,_|\___|_|  |_|\__, |
                |_|                        |___/

""");
                AnsiConsole.MarkupLine($"[cyan bold]Spiderly.CLI v{version}[/]");
                AnsiConsole.WriteLine("-------------------------------------------------");
                AnsiConsole.MarkupLine("Type [cyan]'spiderly help'[/] to see a list of available commands.");
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Unrecognized command.[/]");
                AnsiConsole.MarkupLine("Type [cyan]'spiderly help'[/] to see a list of available commands.");
            }
        }

        private static void ShowHelp()
        {
            HelpCommand.Execute();
        }

        private static bool HasArg(this string[] args, string arg)
        {
            return Array.Exists(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetArgValue(this string[] args, string arg)
        {
            int index = Array.FindIndex(args, a => a.Equals(arg, StringComparison.OrdinalIgnoreCase));
            if (index >= 0 && index + 1 < args.Length)
            {
                return args[index + 1];
            }
            return null;
        }
    }
}
