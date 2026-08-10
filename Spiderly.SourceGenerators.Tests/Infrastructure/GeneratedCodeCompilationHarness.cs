using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spiderly.Shared.Classes;
using Spiderly.Shared.Helpers;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Spiderly.SourceGenerators.Tests.Infrastructure;

/// <summary>
/// Runs every .NET generator over one entity graph and COMPILES the combined output against the real
/// Spiderly assemblies. Separate from <see cref="GeneratorTestHarness"/> because that one only ever calls
/// <c>RunGenerators</c> — it inspects the generator's DIAGNOSTICS and its emitted TEXT, and never compiles the
/// result, so a type error in generated code is invisible to it. It also runs a single generator over an inline
/// source, where the cross-generator references generated code makes (a DTO type, a base service) cannot resolve.
/// <para>
/// That gap is why two CI failures — a <c>CS1061</c> and a <c>CS1503</c> — could only be found by the e2e job
/// building a real scaffolded app, minutes per cycle. This finds the same class of bug in seconds.
/// </para>
/// <para>
/// Entity sources come from two SSOTs and are never copied: the e2e fixture entities (embedded from
/// <c>tests/e2e-fixtures/backend/entities/</c>, so every entity added for the Playwright suite gains compile
/// coverage automatically) and the <c>spiderly init</c> template entities
/// (<see cref="NetAndAngularFilesGenerator.GetEntityFiles"/>, which carry the only <c>[Required]</c>
/// navigation and explicit non-nullable FK scalar anywhere). Fixture files win on a name collision, mirroring
/// what <c>setup.sh</c> does when it overlays them onto a generated app.
/// </para>
/// </summary>
internal static class GeneratedCodeCompilationHarness
{
    internal const string AppName = "TestApp";

    private const string AppNamePlaceholder = "__APP_NAME__";

    private const string FixtureResourcePrefix = "E2eFixtureEntities.";

    /// <summary>
    /// A consumer compiles with the .NET 9 SDK's compiler (C# 13). This project pins
    /// <c>Microsoft.CodeAnalysis.CSharp</c> to the oldest Roslyn the generators must support, whose DEFAULT
    /// language version is older than the code the generators emit — generated services use
    /// <c>nameof(instanceMember)</c>, a C# 12 feature. Parsing at the newest version this Roslyn understands
    /// keeps the harness measuring the generators instead of its own package pin.
    /// </summary>
    private static readonly CSharpParseOptions ParseOptions =
        CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

    /// <summary>
    /// Every generator that emits C#. The Angular generators emit TypeScript and cannot join a C#
    /// compilation, so they are out of scope here (and are covered by their own snapshot tests).
    /// Deliberately no exclusions: an excluded generator is an allowlist with extra steps.
    /// </summary>
    private static readonly IReadOnlyList<IIncrementalGenerator> DotNetGenerators =
    [
        new EntitiesToDTOGenerator(),
        new MapperGenerator(),
        new ServicesGenerator(),
        new ControllerGenerator(),
        new FluentValidationGenerator(),
        new PermissionCodesGenerator(),
        new PaginatedResultGenerator(),
        new ExcelPropertiesGenerator(),
        new AuthorizationServicesGenerator(),
        new EntityValidationGenerator(),
    ];

    /// <summary>
    /// A scaffolded Business project sets <c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c> (see
    /// <c>GetBusinessCsProjData</c>), and generated code relies on it — no generated file emits
    /// <c>using System.Collections.Generic;</c> yet they all use <c>List&lt;T&gt;</c>. A synthetic compilation
    /// gets none of that for free, so without this the harness reports a wall of CS0246 on framework types
    /// and tells you nothing about the generators. This is the Microsoft.NET.Sdk set verbatim.
    /// </summary>
    private const string ImplicitUsings = """
        global using global::System;
        global using global::System.Collections.Generic;
        global using global::System.IO;
        global using global::System.Linq;
        global using global::System.Net.Http;
        global using global::System.Threading;
        global using global::System.Threading.Tasks;
        """;

    /// <summary>
    /// The hand-written partials a scaffolded app supplies for the generators to extend. Only declarations,
    /// no logic — the shapes that carry semantics (entities) come from a real SSOT instead. If a generator
    /// ever needs more of the template than this, the harness goes red, which is the point.
    /// </summary>
    private static readonly string HandWrittenScaffolding = $$"""
        using Spiderly.Shared.Attributes;

        namespace {{AppName}}.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public static partial class Mapper { }
        }

        namespace {{AppName}}.Business.Enums
        {
            [SpiderlyEnum]
            public static partial class PermissionCodes { }
        }
        """;

    /// <summary>
    /// Fixture entities keyed by file name, then template entities for any name the fixtures do not override.
    /// </summary>
    private static IReadOnlyList<string> EntitySources()
    {
        Dictionary<string, string> byFileName = new();

        foreach (SpiderlyFile file in NetAndAngularFilesGenerator.GetEntityFiles(AppName))
            byFileName[file.Name] = file.Data;

        Assembly assembly = typeof(GeneratedCodeCompilationHarness).Assembly;

        foreach (string resource in assembly.GetManifestResourceNames().Where(n => n.StartsWith(FixtureResourcePrefix)))
        {
            using Stream stream = assembly.GetManifestResourceStream(resource)!;
            using StreamReader reader = new(stream);

            // setup.sh:40 does exactly this substitution when overlaying the fixtures onto a generated app.
            byFileName[resource.Substring(FixtureResourcePrefix.Length)] = reader.ReadToEnd().Replace(AppNamePlaceholder, AppName);
        }

        return byFileName.OrderBy(x => x.Key).Select(x => x.Value).ToList();
    }

    /// <summary>
    /// The invariant sources (implicit usings + fixture/template entities + scaffolding), parsed once per
    /// test run. Syntax trees are immutable and safely shared across compilations; the only per-call
    /// variation — <c>NullableContextOptions</c> — lives on the compilation, not the tree. Without this,
    /// every caller re-read the embedded resources and re-parsed ~18 sources (multiple callers per run).
    /// </summary>
    private static readonly Lazy<ImmutableArray<SyntaxTree>> BaseTrees = new(() =>
        new[] { ImplicitUsings }
            .Concat(EntitySources())
            .Append(HandWrittenScaffolding)
            .Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions))
            .ToImmutableArray());

    /// <summary>
    /// Compiles all generator output plus the input sources in ONE compilation, the way a real build does
    /// (no generator sees another's output; the final compilation contains all of it).
    /// </summary>
    /// <param name="generatorDiagnostics">Diagnostics the GENERATORS reported — SPIDERLY### ids and any
    /// generator fault. These do NOT appear in the returned compilation's own diagnostics, so discarding them
    /// let a SPIDERLY028 on the type-zoo fixture pass this harness and fail CI's real build instead.</param>
    internal static Compilation CompileAllGenerators(
        out ImmutableArray<Diagnostic> generatorDiagnostics,
        IEnumerable<string>? extraSources = null,
        NullableContextOptions nullable = NullableContextOptions.Enable)
    {
        IEnumerable<SyntaxTree> trees = BaseTrees.Value
            .Concat((extraSources ?? []).Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)));

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: $"{AppName}.Business",
            syntaxTrees: trees,
            references: TestMetadataReferences.All,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        // parseOptions must be passed explicitly: the driver parses GENERATED trees with its own options,
        // and mixing them with the inputs' language version throws "Inconsistent language versions".
        CSharpGeneratorDriver
            .Create(DotNetGenerators.Select(g => g.AsSourceGenerator()), parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation withGenerated, out generatorDiagnostics);

        return withGenerated;
    }

    /// <summary>
    /// Uniform diagnostic rendering for harness failures (<c>id file(line): message</c>) — one shape for
    /// every consumer of this harness, so failure output doesn't fork per test class.
    /// </summary>
    internal static string Describe(Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        string file = string.IsNullOrEmpty(span.Path) ? "<generated>" : span.Path;

        return $"{diagnostic.Id} {file}{span.StartLinePosition}: {diagnostic.GetMessage()}";
    }
}
