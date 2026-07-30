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
/// Spiderly assemblies. Separate from <see cref="GeneratorTestHarness"/> on purpose: that one deliberately
/// links no Spiderly assembly (its inputs declare marker attributes inline), so emitted references to
/// <c>ServiceBase</c>, <c>IApplicationDbContext</c> or a DTO type are unresolved BY CONSTRUCTION and every
/// type error in generated code is invisible to it.
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
    internal static readonly IReadOnlyList<IIncrementalGenerator> DotNetGenerators =
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
    internal static IReadOnlyList<string> EntitySources()
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
    /// Compiles all generator output plus the input sources in ONE compilation, the way a real build does
    /// (no generator sees another's output; the final compilation contains all of it).
    /// </summary>
    internal static Compilation CompileAllGenerators(
        IEnumerable<string>? extraSources = null,
        NullableContextOptions nullable = NullableContextOptions.Enable)
    {
        List<string> sources = [ImplicitUsings, .. EntitySources(), HandWrittenScaffolding, .. extraSources ?? []];

        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName: $"{AppName}.Business",
            syntaxTrees: sources.Select(s => CSharpSyntaxTree.ParseText(s, ParseOptions)),
            references: FullFrameworkReferences,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithNullableContextOptions(nullable));

        // parseOptions must be passed explicitly: the driver parses GENERATED trees with its own options,
        // and mixing them with the inputs' language version throws "Inconsistent language versions".
        CSharpGeneratorDriver
            .Create(DotNetGenerators.Select(g => g.AsSourceGenerator()), parseOptions: ParseOptions)
            .RunGeneratorsAndUpdateCompilation(compilation, out Compilation withGenerated, out _);

        return withGenerated;
    }

    /// <summary>
    /// True for a syntax tree a generator produced. Generated trees have no on-disk path in this harness, so
    /// membership is decided by identity against the input trees rather than by a path substring.
    /// </summary>
    internal static bool IsGenerated(this Compilation withGenerated, SyntaxTree tree, int inputTreeCount)
        => withGenerated.SyntaxTrees.ToList().IndexOf(tree) >= inputTreeCount;

    /// <summary>
    /// The full runtime closure of THIS test assembly, which now includes Spiderly.Shared / .Security /
    /// .Infrastructure, ASP.NET Core and Mapster because the test project references them. Taking the whole
    /// closure rather than naming assemblies keeps the list from drifting as generated code starts emitting
    /// references to something new.
    /// </summary>
    private static readonly IReadOnlyList<MetadataReference> FullFrameworkReferences = BuildFullReferences();

    private static IReadOnlyList<MetadataReference> BuildFullReferences()
    {
        HashSet<string> paths = new();

        string trustedPlatform = (string)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        foreach (string path in trustedPlatform.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                paths.Add(path);
        }

        return paths.Select(p => (MetadataReference)MetadataReference.CreateFromFile(p)).ToImmutableArray();
    }
}
