namespace Spiderly.ZooGenerator;

/// <summary>
/// Internal build tool. Writes the generated type-zoo fixture (see <see cref="ZooFixtureSource"/>)
/// to the path given by <c>--out</c>. Deterministic output; fails loudly (non-zero exit) on bad
/// arguments so the regen pipeline and CI never half-succeed.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2 || args[0] != "--out")
        {
            Console.Error.WriteLine("Usage: Spiderly.ZooGenerator --out <path/to/ZooShapes.cs>");
            return 1;
        }

        string outPath = args[1];
        string directory = Path.GetDirectoryName(Path.GetFullPath(outPath));
        if (directory == null || !Directory.Exists(directory))
        {
            Console.Error.WriteLine($"Output directory does not exist: {directory}");
            return 1;
        }

        File.WriteAllText(outPath, ZooFixtureSource.Generate());
        Console.WriteLine($"Wrote {outPath}");
        return 0;
    }
}
