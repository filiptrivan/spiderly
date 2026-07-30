using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

internal static class GeneratorTestHarness
{
    private static readonly IReadOnlyList<MetadataReference> References = TestMetadataReferences.All;

    public static GeneratorDriver Run<TGenerator>(string source, NullableContextOptions nullable = NullableContextOptions.Disable, string? spiderlyConfigJson = null)
        where TGenerator : IIncrementalGenerator, new()
        => Run(typeof(TGenerator), source, nullable, spiderlyConfigJson);

    /// <summary>
    /// Type-keyed overload for <c>[Theory]</c> cases, which can't carry a generic type argument.
    /// Instantiates by reflection so there is no per-generator dispatch list to keep in sync with the
    /// theory data — adding a generator to a theory needs no change here.
    /// </summary>
    /// <param name="spiderlyConfigJson">Contents of the consumer's <c>.spiderly/config.json</c>, supplied as
    /// an <see cref="AdditionalText"/> exactly as MSBuild does. Omit for the default config (every generator
    /// enabled) — which is what every test did before this existed, meaning config gating had no end-to-end
    /// coverage at all.</param>
    public static GeneratorDriver Run(Type generatorType, string source, NullableContextOptions nullable = NullableContextOptions.Disable, string? spiderlyConfigJson = null)
    {
        IIncrementalGenerator generator = (IIncrementalGenerator)Activator.CreateInstance(generatorType)!;

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: "Spiderly.SourceGenerators.Tests.Fixture",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source)],
            references: References,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        ImmutableArray<AdditionalText> additionalTexts = spiderlyConfigJson is null
            ? ImmutableArray<AdditionalText>.Empty
            // Extensions.GetSpiderlyConfig matches on a path ENDING in ".spiderly/config.json"
            // (separator-normalized), so this must keep that suffix to be picked up.
            : [new InMemoryAdditionalText(".spiderly/config.json", spiderlyConfigJson)];

        return CSharpGeneratorDriver
            .Create([generator.AsSourceGenerator()], additionalTexts)
            .RunGenerators(compilation);
    }

    private sealed class InMemoryAdditionalText : AdditionalText
    {
        private readonly SourceText _text;

        public InMemoryAdditionalText(string path, string content)
        {
            Path = path;
            _text = SourceText.From(content);
        }

        public override string Path { get; }

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
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

}
