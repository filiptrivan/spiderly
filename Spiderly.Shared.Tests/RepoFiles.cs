namespace Spiderly.Shared.Tests
{
    /// <summary>Reads a file of this repo that the tests check but do not build, such as the Angular sources.</summary>
    internal static class RepoFiles
    {
        /// <summary>The text of <paramref name="relativePath"/>, a path from the repo root with <c>/</c> separators.</summary>
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
