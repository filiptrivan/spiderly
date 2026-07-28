using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Spiderly.CLI.Services
{
    internal static class ProcessRunner
    {
        public static async Task<(bool success, string output)> RunShellCommand(string command, string? workingDirectory = null)
        {
            bool isWin = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            string shell = isWin ? "cmd.exe" : "/bin/bash";
            string args = isWin ? $"/c {command}" : $"-c \"{command}\"";

            return await RunCommand(shell, args, workingDirectory);
        }

        public static async Task<bool> IsCommandAvailable(string shellFileName, string whichCommand)
        {
            try
            {
                (bool success, string _) = await RunCommand(shellFileName, whichCommand);
                return success;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<(bool success, string output)> RunCommand(
            string shellFileName,
            string arguments,
            string? workingDirectory = null)
        {
            if (workingDirectory == null)
                workingDirectory = Environment.CurrentDirectory;

            using (Process process = new Process())
            {
                System.Text.StringBuilder outputBuilder = new System.Text.StringBuilder();

                process.StartInfo = new ProcessStartInfo
                {
                    FileName = shellFileName,
                    Arguments = arguments,
                    WorkingDirectory = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        Console.WriteLine(e.Data); // We can't use AnsiConsole here because of the unexpected markup
                        outputBuilder.AppendLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                        Console.WriteLine(e.Data); // We can't use AnsiConsole here because of the unexpected markup
                };

                process.Start();

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await process.WaitForExitAsync();

                return (process.ExitCode == 0, outputBuilder.ToString());
            }
        }
    }
}
