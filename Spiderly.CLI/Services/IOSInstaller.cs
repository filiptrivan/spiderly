namespace Spiderly.CLI.Services
{
    internal interface IOSInstaller
    {
        Task<bool> InstallPostgreSQL();
        Task<bool> InstallSqlServer();
        Task<bool> IsCommandAvailable(string command);
        Task<bool> IsPostgreSQLServiceRunning();
        Task<bool> IsSqlServerServiceRunning();
    }
}
