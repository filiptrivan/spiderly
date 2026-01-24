using Spiderly.CLI.Services;

namespace Spiderly.CLI.Commands
{
    internal static class MigrationCommand
    {
        public static async Task<int> AddMigration(string migrationName, string backendPath = null)
        {
            if (string.IsNullOrWhiteSpace(migrationName))
            {
                ConsoleHelper.MarkupLineERROR("Migration name is required. Usage: spiderly add-migration <MigrationName>");
                return 1;
            }

            return await RunEfCommand($"migrations add {migrationName}", "Creating migration", backendPath);
        }

        public static async Task<int> UpdateDatabase(string backendPath = null)
        {
            return await RunEfCommand("database update", "Updating database", backendPath);
        }

        public static async Task<int> RemoveMigration()
        {
            return await RunEfCommand("migrations remove", "Removing migration");
        }

        public static async Task<int> ListMigrations()
        {
            return await RunEfCommand("migrations list", "Listing migrations");
        }

        private static async Task<int> RunEfCommand(string efArgs, string operationName, string backendPath = null)
        {
            backendPath ??= FindBackendPath();

            if (backendPath == null)
            {
                ConsoleHelper.MarkupLineERROR("Could not find Backend folder. Please run this command from within your Spiderly project directory.");
                return 1;
            }

            (string infrastructurePath, string webApiCsprojRelativePath) = FindProjectPaths(backendPath);

            if (infrastructurePath == null || webApiCsprojRelativePath == null)
            {
                return 1;
            }

            string infrastructureCsprojPath = Directory.GetFiles(infrastructurePath, "*.Infrastructure.csproj").First();
            string infrastructureCsprojRelativePath = Path.Combine(".", Path.GetFileName(infrastructureCsprojPath));

            string fullArgs = $"ef {efArgs} --project {infrastructureCsprojRelativePath} --startup-project {webApiCsprojRelativePath}";

            ConsoleHelper.MarkupLineLoading($"{operationName}...");
            (bool success, _) = await ProcessRunner.RunCommand("dotnet", fullArgs, infrastructurePath);

            return success ? 0 : 1;
        }

        private static (string infrastructurePath, string webApiCsprojRelativePath) FindProjectPaths(string backendPath)
        {
            string infrastructurePath = Directory.GetDirectories(backendPath, "*.Infrastructure").FirstOrDefault();
            if (infrastructurePath == null)
            {
                ConsoleHelper.MarkupLineERROR("Could not find *.Infrastructure folder in Backend. Please run this command from within your Spiderly project directory.");
                return (null, null);
            }

            string webApiPath = Directory.GetDirectories(backendPath, "*.WebAPI").FirstOrDefault();
            if (webApiPath == null)
            {
                ConsoleHelper.MarkupLineERROR("Could not find *.WebAPI folder in Backend. Please run this command from within your Spiderly project directory.");
                return (null, null);
            }

            string webApiCsprojPath = Directory.GetFiles(webApiPath, "*.WebAPI.csproj").FirstOrDefault();
            if (webApiCsprojPath == null)
            {
                ConsoleHelper.MarkupLineERROR("Could not find *.WebAPI.csproj. Please run this command from within your Spiderly project directory.");
                return (null, null);
            }

            string webApiCsprojRelativePath = Path.GetRelativePath(infrastructurePath, webApiCsprojPath);

            return (infrastructurePath, webApiCsprojRelativePath);
        }

        private static string FindBackendPath()
        {
            string currentDir = Environment.CurrentDirectory;

            if (Path.GetFileName(currentDir) == "Backend")
                return currentDir;

            string backendInCurrent = Path.Combine(currentDir, "Backend");
            if (Directory.Exists(backendInCurrent))
                return backendInCurrent;

            string parentDir = Directory.GetParent(currentDir)?.FullName;
            if (parentDir != null && Path.GetFileName(parentDir) == "Backend")
                return parentDir;

            return null;
        }
    }
}