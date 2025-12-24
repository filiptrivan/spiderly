using Spectre.Console;

namespace Spiderly.CLI.Services
{
    internal static class ConsoleHelper
    {
        public static bool PromptYesNo(string message)
        {
            return AnsiConsole.Confirm(message);
        }

        public static void MarkupLineOK(string message)
        {
            AnsiConsole.MarkupLine($"[green][[OK]][/] {message}");
        }

        public static void MarkupLineWARNING(string message)
        {
            AnsiConsole.MarkupLine($"[yellow][[WARNING]][/] {message}");
        }

        public static void MarkupLineERROR(string message)
        {
            AnsiConsole.MarkupLine($"[red][[ERROR]][/] {message}");
        }
    }
}
