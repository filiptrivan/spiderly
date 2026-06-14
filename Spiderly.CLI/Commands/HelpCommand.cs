using Spectre.Console;

namespace Spiderly.CLI.Commands
{
    internal static class HelpCommand
    {
        public static void Execute()
        {
            AnsiConsole.MarkupLine("\n[bold]Usage:[/] spiderly [cyan bold][[command]][/] [yellow bold][[options]][/]\n");

            List<CommandInfo> commands = new List<CommandInfo>
            {
                new CommandInfo
                {
                    Name = "init",
                    Description = "This command initializes a new Spiderly project with a .NET backend and an Angular frontend. You can run it using [bold]spiderly init[/], and we will dynamically ask you for all the required information.",
                    Options = new List<OptionInfo>
                    {
                        new OptionInfo
                        {
                            Name = "--name",
                            Description = "App name without spaces (you can change it later)."
                        },
                        new OptionInfo
                        {
                            Name = "--db",
                            Description = "Database provider: sqlserver or postgresql (you can change it later)."
                        },
                        new OptionInfo
                        {
                            Name = "--db-connection-string",
                            Description = "Full EF Core connection string. Bypasses auto-discovery and Docker."
                        },
                        new OptionInfo
                        {
                            Name = "--pm",
                            Description = "Package manager: npm (default), pnpm, yarn, or bun."
                        },
                    },
                    Examples = new List<string>
                    {
                        "spiderly init",
                        "spiderly init --name MyApp --db postgresql",
                        "spiderly init --name MyApp --db postgresql --pm pnpm",
                        "spiderly init --name MyApp --db postgresql --db-connection-string \"Host=localhost;Port=5432;Database=myapp;Username=postgres;Password=secret\""
                    }
                },
                new CommandInfo
                {
                    Name = "add-new-entity",
                    Description = "This command creates a new entity and generates all necessary files: Entity class, Angular pages (list/details), routes, and menu item.",
                    Options = new List<OptionInfo>
                    {
                        new OptionInfo
                        {
                            Name = "--name",
                            Description = "Entity name in PascalCase (e.g., Product). Required in non-interactive mode."
                        },
                        new OptionInfo
                        {
                            Name = "--data-view",
                            Description = "Generate DataView template instead of Table template."
                        }
                    },
                    Examples = new List<string>
                    {
                        "spiderly add-new-entity",
                        "spiderly add-new-entity --name Product",
                        "spiderly add-new-entity --name Product --data-view"
                    }
                },
                new CommandInfo
                {
                    Name = "add-migration",
                    Description = "Create a new EF Core migration.",
                    Options = new List<OptionInfo>
                    {
                        new OptionInfo
                        {
                            Name = "<name>",
                            Description = "The name for the new migration."
                        }
                    },
                    Examples = new List<string>
                    {
                        "spiderly add-migration YourMigrationName"
                    }
                },
                new CommandInfo
                {
                    Name = "update-database",
                    Description = "Apply pending migrations to the database.",
                    Examples = new List<string>
                    {
                        "spiderly update-database"
                    }
                },
                new CommandInfo
                {
                    Name = "remove-migration",
                    Description = "Remove the last migration.",
                    Examples = new List<string>
                    {
                        "spiderly remove-migration"
                    }
                },
                new CommandInfo
                {
                    Name = "list-migrations",
                    Description = "List all available migrations.",
                    Examples = new List<string>
                    {
                        "spiderly list-migrations"
                    }
                },
                new CommandInfo
                {
                    Name = "agent-sync",
                    Description = "Sync version-matched Spiderly AI-agent guidance from the installed npm package into this project (writes an AGENTS.md index and makes CLAUDE.md import it). Idempotent; safe to re-run after upgrades.",
                    Examples = new List<string>
                    {
                        "spiderly agent-sync"
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
                return parts[0];

            if (parts.Length == 2)
                return $"{parts[0]} [cyan]{parts[1]}[/]";

            string firstPart = parts[0];
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
