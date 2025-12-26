using Spiderly.CLI.Services.Database.OS;
using Spiderly.Shared.Helpers;

namespace Spiderly.CLI.Services.Database.DbConnectionStringBuilder
{
    public class SQLServerConnectionStringBuilder : BaseDbConnectionStringBuilder
    {
        protected override string DbProviderName => "SQL Server";
        protected override string ManualInstallUrl => "https://www.microsoft.com/en-us/sql-server/sql-server-downloads";

        public SQLServerConnectionStringBuilder(BaseOSInstaller installer) : base(installer)
        {
        }

        protected override async Task<bool> InstallDatabaseProvider()
        {
            return await Installer.InstallSqlServer();
        }

        protected override string CreateDatabaseConnectionString(string appName)
        {
            return Helper.CreateSqlServerConnectionString(appName);
        }

        protected override async Task<bool> IsDatabaseServiceRunning()
        {
            return await Installer.IsSqlServerServiceRunning();
        }
    }
}
