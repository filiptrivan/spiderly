namespace Spiderly.CLI.Services
{
    internal static class DotnetEfToolService
    {
        public static async Task<bool> EnsureDotnetEfAvailable(string backendPath)
        {
            ConsoleHelper.MarkupLineLoading("Restoring dotnet tools...");

            (bool success, string _) = await ProcessRunner.RunCommand(
                "dotnet",
                "tool restore",
                backendPath
            );

            return success;
        }
    }
}
