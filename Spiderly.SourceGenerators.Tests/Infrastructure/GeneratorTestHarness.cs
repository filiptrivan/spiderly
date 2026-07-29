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

    public static GeneratorDriver Run<TGenerator>(string source, NullableContextOptions nullable = NullableContextOptions.Disable)
        where TGenerator : IIncrementalGenerator, new()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.Fixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        return CSharpGeneratorDriver.Create(new TGenerator()).RunGenerators(compilation);
    }

    /// <summary>
    /// Runs <typeparamref name="TGenerator"/> and returns the compilation INCLUDING its generated
    /// output, so a test can assert on diagnostics the emitted code itself raises (a consumer can't
    /// edit generated files, so warnings there are the framework's bug, not theirs).
    /// </summary>
    public static Compilation CompileGeneratedOutput<TGenerator>(string source, NullableContextOptions nullable = NullableContextOptions.Disable)
        where TGenerator : IIncrementalGenerator, new()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.Fixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        CSharpGeneratorDriver.Create(new TGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation withGenerated, out _);

        return withGenerated;
    }

    /// <summary>
    /// Builds a compilation whose entities live in a *referenced* compilation rather than its own source — the
    /// metadata path exercised by <see cref="Spiderly.SourceGenerators.Shared.ReferencedAssemblyAnalyzer"/> (e.g.
    /// PACMS.WebAPI referencing PACMS.Business). <paramref name="referencedSource"/> is compiled to its own assembly
    /// and attached via <see cref="Compilation.ToMetadataReference"/>, so the analyzer sees real symbols (a generic
    /// base like <c>BusinessObject&lt;long&gt;</c>, <c>List&lt;Foo&gt;</c> properties) exactly as it would in a build.
    /// Unlike the inline tests, the referenced source must actually compile, so it declares the marker attributes and
    /// base types it uses.
    /// </summary>
    public static Compilation CreateCompilationWithReference(string mainSource, string referencedSource, NullableContextOptions nullable = NullableContextOptions.Disable)
    {
        CSharpCompilation referencedCompilation = CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.ReferencedFixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(referencedSource)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        return CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.MainFixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(mainSource)],
            references: References.Append(referencedCompilation.ToMetadataReference()),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));
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
