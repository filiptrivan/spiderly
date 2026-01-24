namespace Spiderly.CLI.Services
{
    internal static class DotnetEfToolService
    {
        private const string RequiredVersion = "9.0.1";

        public static async Task<bool> EnsureDotnetEfAvailable(string backendPath)
        {
            if (!CreateToolManifest(backendPath))
                return false;

            return await RestoreTools(backendPath);
        }

        private static bool CreateToolManifest(string backendPath)
        {
            string configPath = Path.Combine(backendPath, ".config");
            string manifestPath = Path.Combine(configPath, "dotnet-tools.json");

            try
            {
                Directory.CreateDirectory(configPath);

                string manifestContent = $$"""
                    {
                      "version": 1,
                      "isRoot": true,
                      "tools": {
                        "dotnet-ef": {
                          "version": "{{RequiredVersion}}",
                          "commands": [
                            "dotnet-ef"
                          ]
                        }
                      }
                    }
                    """;

                File.WriteAllText(manifestPath, manifestContent);
                return true;
            }
            catch (Exception ex)
            {
                ConsoleHelper.MarkupLineERROR($"Failed to create tool manifest: {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> RestoreTools(string backendPath)
        {
            ConsoleHelper.MarkupLineLoading("Restoring dotnet tools...");

            (bool success, string _) = await ProcessRunner.RunCommand(
                "dotnet",
                "tool restore",
                backendPath
            );

            if (!success)
            {
                return false;
            }

            return true;
        }
    }
}
