using Spiderly.Shared.Enums;

namespace Spiderly.CLI.Services
{
    internal static class PrerequisiteChecker
    {
        public static async Task<bool> ValidatePrerequisites(PackageManagerCodes packageManager)
        {
            bool allPassed = true;

            if (!await CheckTool("dotnet --version", ".NET SDK", minimumMajorVersion: 9, "https://dotnet.microsoft.com/en-us/download/dotnet/9.0", isRequired: true))
                allPassed = false;

            if (!await CheckTool("node --version", "Node.js", minimumMajorVersion: 18, "https://nodejs.org/en/download/", isRequired: true))
                allPassed = false;

            if (packageManager != PackageManagerCodes.Npm)
            {
                string pmName = packageManager.GetCommandName();

                string pmUrl = packageManager switch
                {
                    PackageManagerCodes.Pnpm => "https://pnpm.io/installation",
                    PackageManagerCodes.Yarn => "https://yarnpkg.com/getting-started/install",
                    PackageManagerCodes.Bun => "https://bun.sh/docs/installation",
                    _ => throw new ArgumentOutOfRangeException(nameof(packageManager), packageManager, null)
                };

                if (!await CheckTool($"{pmName} --version", pmName, minimumMajorVersion: null, pmUrl, isRequired: true))
                    allPassed = false;
            }

            await CheckTool("docker --version", "Docker", minimumMajorVersion: null, "https://docs.docker.com/get-docker/", isRequired: false);

            return allPassed;
        }

        private static async Task<bool> CheckTool(string command, string displayName, int? minimumMajorVersion, string installUrl, bool isRequired)
        {
            (bool success, string output) = await ProcessRunner.RunShellCommand(command);

            if (!success || string.IsNullOrWhiteSpace(output))
            {
                string message = $"{displayName} is not installed. Install from: [link]{installUrl}[/]";

                if (isRequired)
                    ConsoleHelper.MarkupLineERROR(message);
                else
                    ConsoleHelper.MarkupLineWARNING($"{displayName} is not installed. {displayName} is optional but recommended for automatic database setup. Install from: [link]{installUrl}[/]");

                return !isRequired;
            }

            string versionString = GetFirstLine(output);
            string cleanVersion = versionString.TrimStart('v');

            if (minimumMajorVersion != null && Version.TryParse(ExtractVersionNumber(cleanVersion), out Version version))
            {
                if (version.Major < minimumMajorVersion.Value)
                {
                    ConsoleHelper.MarkupLineERROR($"{displayName} {versionString} found, but {minimumMajorVersion}.0 or later is required. Install from: [link]{installUrl}[/]");
                    return false;
                }
            }

            ConsoleHelper.MarkupLineOK($"{displayName} {versionString}");
            return true;
        }

        private static string GetFirstLine(string output)
        {
            return output.Trim().Split('\n')[0].Trim();
        }

        private static string ExtractVersionNumber(string input)
        {
            int endIndex = 0;

            foreach (char c in input)
            {
                if (char.IsDigit(c) || c == '.')
                    endIndex++;
                else
                    break;
            }

            return input.Substring(0, endIndex);
        }
    }
}
