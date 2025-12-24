using Spectre.Console;

namespace Spiderly.CLI.Commands
{
    internal static class HelpCommand
    {
        public static void Execute()
        {
            AnsiConsole.MarkupLine("\n[bold]Usage:[/] [dim]spiderly[/] [cyan bold][[command]][/] [yellow bold][[options]][/]\n");

            List<CommandInfo> commands = new List<CommandInfo>
            {
                new CommandInfo
                {
                    Name = "init",
                    Description = "This command initializes a new Spiderly project with .NET backend and Angular frontend",
                    Options = new List<OptionInfo>
                    {
                        new OptionInfo
                        {
                            Name = "--name",
                            Description = "App name without spaces (required in non-interactive mode, e.g. GitHub workflows)"
                        },
                        new OptionInfo
                        {
                            Name = "--db",
                            Description = "Database provider: sqlserver or postgresql (required in non-interactive mode, e.g. GitHub workflows)"
                        },
                        new OptionInfo
                        {
                            Name = "--top-menu",
                            Description = "Use a top menu layout instead of the default side menu layout"
                        }
                    },
                    Examples = new List<string>
                    {
                        "spiderly init",
                        "spiderly init --top-menu",
                        "spiderly init --name MyApp --db sqlserver",
                        "spiderly init --name MyApp --db postgresql --top-menu"
                    }
                },
                new CommandInfo
                {
                    Name = "add-new-page",
                    Description = "This command generates starter files to support CRUD operations for a new entity",
                    Options = new List<OptionInfo>
                    {
                        new OptionInfo
                        {
                            Name = "--data-view",
                            Description = "Generate DataView template instead of Table template"
                        }
                    },
                    Examples = new List<string>
                    {
                        "spiderly add-new-page",
                        "spiderly add-new-page --data-view"
                    }
                }
            };

            foreach (CommandInfo command in commands)
            {
                RenderCommands(command);
                AnsiConsole.WriteLine();
            }
        }

        private static void RenderCommands(CommandInfo command)
        {
            AnsiConsole.MarkupLine($"* [cyan bold]{command.Name}[/]");
            AnsiConsole.MarkupLine($"  {command.Description}");

            if (command.Options.Count > 0)
            {
                AnsiConsole.WriteLine();
                foreach (OptionInfo option in command.Options)
                {
                    AnsiConsole.MarkupLine($"  [yellow]{option.Name}[/]  {option.Description}");
                }
            }

            if (command.Examples.Count > 0)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(command.Examples.Count == 1 ? "  [white bold]Example:[/]" : "  [white bold]Examples:[/]");
                foreach (string example in command.Examples)
                {
                    string coloredExample = ColorizeExample(example);
                    AnsiConsole.MarkupLine($"    [dim]$[/] {coloredExample}");
                }
            }
        }

        private static string ColorizeExample(string example)
        {
            string[] parts = example.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length == 0)
                return example;

            if (parts.Length == 1)
                return $"[dim]{parts[0]}[/]";

            if (parts.Length == 2)
                return $"[dim]{parts[0]}[/] [cyan]{parts[1]}[/]";

            string firstPart = $"[dim]{parts[0]}[/]";
            string secondPart = $"[cyan]{parts[1]}[/]";
            string remainingParts = string.Join(" ", parts.Skip(2));

            return $"{firstPart} {secondPart} [yellow]{remainingParts}[/]";
        }

        private class CommandInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
            public List<OptionInfo> Options { get; set; } = new();
            public List<string> Examples { get; set; } = new();
        }

        private class OptionInfo
        {
            public string Name { get; set; }
            public string Description { get; set; }
        }
    }
}
