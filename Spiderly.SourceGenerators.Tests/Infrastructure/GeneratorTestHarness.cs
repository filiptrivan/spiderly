using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

internal static class GeneratorTestHarness
{
    // Spiderly attributes are matched by syntax name only, so test inputs declare
    // `[SpiderlyEntity]` etc. inline rather than referencing the full Spiderly.Shared assembly.
    private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

    public static GeneratorDriver Run<TGenerator>(string source)
        where TGenerator : IIncrementalGenerator, new()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.Fixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new TGenerator()).RunGenerators(compilation);
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        HashSet<string> paths = new()
        {
            typeof(object).Assembly.Location,
            typeof(List<>).Assembly.Location,
            typeof(System.Linq.Enumerable).Assembly.Location,
            typeof(System.ComponentModel.DataAnnotations.Schema.ForeignKeyAttribute).Assembly.Location,
        };

        // Pull in the rest of the BCL so anything the input source happens to reference
        // (Guid, DateTime, …) resolves cleanly; otherwise CS-warnings leak into the snapshot's diagnostics.
        string trustedPlatform = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string path in trustedPlatform.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                paths.Add(path);
        }

        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToList();
    }
}
