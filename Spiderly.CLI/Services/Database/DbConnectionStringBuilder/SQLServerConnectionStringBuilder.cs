using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public class SQLServerConnectionStringBuilder : BaseDbConnectionStringBuilder
    {
        protected override string DbProviderName => "SQL Server";
        protected override string ManualInstallUrl => "https://www.microsoft.com/en-us/sql-server/sql-server-downloads";

        protected override string DockerComposeContent => """
            services:
              db:
                image: mcr.microsoft.com/mssql/server:2022-latest
                container_name: spiderly-sqlserver
                environment:
                  ACCEPT_EULA: "Y"
                  MSSQL_SA_PASSWORD: "SqlServer123"
                ports:
                  - "14330:1433"
                volumes:
                  - sqlserver_data:/var/opt/mssql

            volumes:
              sqlserver_data:
            """.Replace("            ", "");

        protected override string CreateDatabaseConnectionString(string appName)
        {
            return Helper.CreateSqlServerConnectionString(appName);
        }
    }
}
