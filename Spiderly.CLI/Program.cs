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
        private static async Task<int> Main(string[] args)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string fullVersion = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;
            string version = fullVersion?.Split('+')[0]; // If we don't split, it will return the full version with the commit hash, which is not needed for the init command.

            if (args.HasArg("--help") || args.HasArg("-help") || args.HasArg("help"))
            {
                ShowHelp();
                return 0;
            }
            else if (args.HasArg("init"))
            {
                bool isRunningFromNuget = !args.HasArg("--dev");
                string appName = args.GetArgValue("--name");
                string dbProvider = args.GetArgValue("--db");
                string dbConnectionString = args.GetArgValue("--db-connection-string");
                string packageManager = args.GetArgValue("--pm");

                return await InitCommand.Execute(isRunningFromNuget, version, appName, dbProvider, dbConnectionString, packageManager);
            }
            else if (args.HasArg("add-new-entity"))
            {
                bool shouldGenerateDataView = args.HasArg("--data-view");
                string entityName = args.GetArgValue("--name");

                return await AddNewEntityCommand.Execute(shouldGenerateDataView, entityName);
            }
            else if (args.HasArg("add-migration"))
            {
                string migrationName = args.GetArgValue("add-migration");
                return await MigrationCommand.AddMigration(migrationName);
            }
            else if (args.HasArg("update-database"))
            {
                return await MigrationCommand.UpdateDatabase();
            }
            else if (args.HasArg("remove-migration"))
            {
                return await MigrationCommand.RemoveMigration();
            }
            else if (args.HasArg("list-migrations"))
            {
                return await MigrationCommand.ListMigrations();
            }
            else if (args.HasArg("agent-sync"))
            {
                return AgentSyncCommand.Execute();
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
                return 0;
            }
            else
            {
                AnsiConsole.MarkupLine("[red]✗ Unrecognized command.[/]");
                AnsiConsole.MarkupLine("Type [cyan]'spiderly help'[/] to see a list of available commands.");
                return 1;
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
