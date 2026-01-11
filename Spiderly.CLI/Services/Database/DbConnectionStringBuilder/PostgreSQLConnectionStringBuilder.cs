using Spectre.Console;
using Spiderly.CLI.Services.Database.OS;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public class PostgreSQLConnectionStringBuilder : BaseDbConnectionStringBuilder
    {
        protected override string DbProviderName => "PostgreSQL";
        protected override string ManualInstallUrl => "https://www.postgresql.org/download/";

        public PostgreSQLConnectionStringBuilder(BaseOSInstaller installer) : base(installer)
        {
        }

        protected override async Task<bool> IsDatabaseServiceRunning()
        {
            return await Installer.IsPostgreSQLServiceRunning();
        }

        protected override async Task<bool> InstallDatabaseProvider()
        {
            return await Installer.InstallPostgreSQL();
        }

        protected override string CreateDatabaseConnectionString(string appName)
        {
            string connectionString = Helper.CreatePostgreSQLConnectionString(appName);

            if (connectionString == null)
            {
                ConsoleHelper.MarkupLineWARNING("We tried to connect to the database with a couple of standard passwords without success. Please enter the password for the 'postgres' user:");

                while (connectionString == null)
                {
                    string password = AnsiConsole.Prompt(new TextPrompt<string>("Password (or type 'exit' to stop):"));

                    if (password.Equals("exit", StringComparison.OrdinalIgnoreCase))
                    {
                        AnsiConsole.MarkupLine("Password entry cancelled.");
                        break;
                    }

                    connectionString = Helper.CreatePostgreSQLConnectionString(appName, password);

                    if (connectionString == null)
                    {
                        ConsoleHelper.MarkupLineERROR("Invalid password. Please try again.");
                    }
                }
            }

            return connectionString;
        }

    }
}
