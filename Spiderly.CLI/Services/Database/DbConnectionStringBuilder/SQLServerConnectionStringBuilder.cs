using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public class SQLServerConnectionStringBuilder : BaseDbConnectionStringBuilder
    {
        protected override string DbProviderName => "SQL Server";
        protected override string ManualInstallUrl => "https://www.microsoft.com/en-us/sql-server/sql-server-downloads";

        protected override string DockerRunArguments => "run --name spiderly-sqlserver -e ACCEPT_EULA=Y -e \"MSSQL_SA_PASSWORD=SqlServer123\" -p 14330:1433 -v spiderly_sqlserver_data:/var/opt/mssql -d mcr.microsoft.com/mssql/server:2022-latest";

        protected override string? CreateDatabaseConnectionString(string appName)
        {
            return Helper.CreateSqlServerConnectionString(appName);
        }
    }
}
