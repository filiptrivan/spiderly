using System;
using System.IO;

namespace Spiderly.Shared.Tests
{
    /// <summary>
    /// Reads a file of this repo by its path from the repo root, the folder that holds <c>Spiderly.sln</c>, so
    /// a test can pin a non-C# file (a TypeScript mirror, <c>Angular/package.json</c>) against the C# it mirrors.
    /// </summary>
    internal static class RepoFiles
    {
        internal static string Read(string relativePath)
        {
            DirectoryInfo? dir = new(AppContext.BaseDirectory);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Spiderly.sln")))
                dir = dir.Parent;

            if (dir is null)
                throw new DirectoryNotFoundException(
                    $"Could not locate the repo root (Spiderly.sln) above {AppContext.BaseDirectory}.");

            string path = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
                throw new FileNotFoundException($"Expected repo file not found: {path}");

            return File.ReadAllText(path);
        }
    }
}
