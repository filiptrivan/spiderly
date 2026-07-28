using Spectre.Console;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public class PostgreSQLConnectionStringBuilder : BaseDbConnectionStringBuilder
    {
        protected override string DbProviderName => "PostgreSQL";
        protected override string ManualInstallUrl => "https://www.postgresql.org/download/";

        protected override string DockerRunArguments => "run --name spiderly-postgres -e POSTGRES_PASSWORD=postgres -p 54320:5432 -v spiderly_postgres_data:/var/lib/postgresql/data -d postgres:latest";

        protected override string? CreateDatabaseConnectionString(string appName)
        {
            string? connectionString = Helper.CreatePostgreSQLConnectionString(appName);

            if (connectionString != null)
            {
                return connectionString;
            }

            if (!ConsoleHelper.IsInteractive())
            {
                return null;
            }

            ConsoleHelper.MarkupLineWARNING("We tried to connect to the database with a couple of standard passwords without success. Please enter the password for the 'postgres' user:");

            while (connectionString == null)
            {
                string password = AnsiConsole.Prompt(new TextPrompt<string>("Password:"));

                connectionString = Helper.CreatePostgreSQLConnectionString(appName, password);

                if (connectionString == null)
                {
                    ConsoleHelper.MarkupLineERROR("Invalid password. Please try again.");
                }
            }

            return connectionString;
        }
    }
}
