namespace Spiderly.CLI.Services.Database.OS
{
    internal static class DockerInstaller
    {
        public static async Task<bool> InstallPostgreSQLDocker()
        {
            ConsoleHelper.MarkupLineLoading("Installing PostgreSQL via Docker...");
            Console.WriteLine("This may take several minutes...");

            string dockerCommand = "run --name spiderly-postgres -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:latest";

            (bool successful, _) = await ProcessRunner.RunCommand("docker", dockerCommand);

            if (successful)
            {
                ConsoleHelper.MarkupLineOK("PostgreSQL container has been started.");
                Console.WriteLine("Connection details:");
                Console.WriteLine("  Host: localhost");
                Console.WriteLine("  Port: 5432");
                Console.WriteLine("  Username: postgres");
                Console.WriteLine("  Password: postgres");
                return true;
            }

            return false;
        }

        public static async Task<bool> InstallSqlServerDocker(BaseOSInstaller installer, Func<Task<bool>> installDocker)
        {
            if (!await installer.IsCommandAvailable("docker"))
            {
                ConsoleHelper.MarkupLineWARNING("Docker is not installed.");

                if (!ConsoleHelper.PromptYesNo("Would you like to install Docker to run SQL Server in a container?"))
                {
                    return false;
                }

                if (await installDocker())
                {
                    ConsoleHelper.MarkupLineOK("Docker installed successfully! Now installing SQL Server...");
                    return await InstallSqlServerDockerContainer();
                }

                return false;
            }

            return await InstallSqlServerDockerContainer();
        }

        private static async Task<bool> InstallSqlServerDockerContainer()
        {
            ConsoleHelper.MarkupLineLoading("Docker detected. Installing SQL Server via Docker...");
            Console.WriteLine("This may take several minutes...");

            string dockerCommand = "run -e \"ACCEPT_EULA=Y\" -e \"SA_PASSWORD=YourPassword123.\" -p 1433:1433 --name spiderly-sqlserver -d mcr.microsoft.com/mssql/server:2022-latest";

            (bool successful, _) = await ProcessRunner.RunCommand("docker", dockerCommand);

            if (successful)
            {
                ConsoleHelper.MarkupLineOK("SQL Server container has been started.");
                Console.WriteLine("Connection details:");
                Console.WriteLine("  Server: localhost,1433");
                Console.WriteLine("  Username: sa");
                Console.WriteLine("  Password: YourPassword123.");
                return true;
            }

            return false;
        }
    }
}
