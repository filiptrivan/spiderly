using System.Runtime.Versioning;
using System.Security.Principal;
using Spectre.Console;

namespace Spiderly.CLI.Services
{
    internal static class ConsoleHelper
    {
        public static bool PromptYesNo(string message)
        {
            return AnsiConsole.Prompt(
                new TextPrompt<bool>($"{message} [[y/n]]")
                    .AddChoice(true)
                    .AddChoice(false)
                    .WithConverter(choice => choice ? "y" : "n"));
        }

        public static void MarkupLineLoading(string message)
        {
            AnsiConsole.MarkupLine($"\n[dim]{message}[/]");
        }

        public static void MarkupLineOK(string message)
        {
            AnsiConsole.MarkupLine($"\n[green][[OK]][/] {message}");
        }

        public static void MarkupLineWARNING(string message)
        {
            AnsiConsole.MarkupLine($"\n[yellow][[WARNING]][/] {message}");
        }

        public static void MarkupLineERROR(string message)
        {
            AnsiConsole.MarkupLine($"\n[red][[ERROR]][/] {message}");
        }

        public static bool IsInteractive()
        {
            return !Console.IsInputRedirected && Environment.UserInteractive;
        }

    }
}
