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
                ConsoleHelper.MarkupLineWARNING("Unable to connect to PostgreSQL with standard credentials. Please enter the password for the 'postgres' user (or press Enter to skip and configure the connection string manually later):");

                string password = AnsiConsole.Prompt(new TextPrompt<string>("Password:"));

                if (!string.IsNullOrWhiteSpace(password))
                {
                    connectionString = Helper.CreatePostgreSQLConnectionString(appName, password);
                }
            }

            return connectionString;
        }

    }
}
