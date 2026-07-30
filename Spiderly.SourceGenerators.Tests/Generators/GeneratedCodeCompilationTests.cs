using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// The gap this closes: generator unit tests snapshot emitted TEXT and never compile it, so a generator
/// emitting <c>.Value</c> on a non-nullable column, or a member the DTO does not have, is green locally and
/// only fails in CI's e2e job minutes later. See <see cref="GeneratedCodeCompilationHarness"/> for why
/// <see cref="GeneratorTestHarness"/> structurally cannot be widened to catch it.
/// <para>
/// NO ALLOWLIST, by decision. Not a diagnostic-id filter, not a per-generator exclusion. A warning in
/// generated code is the framework's bug, because a consumer cannot edit the file to suppress it and
/// <c>&lt;WarningsAsErrors&gt;Nullable&lt;/WarningsAsErrors&gt;</c> already makes a subset fatal for them. An
/// error in the fixture means the fixture is wrong. Every filter added past this point recreates the
/// situation being fixed, where a green local suite meant nothing.
/// </para>
/// </summary>
public class GeneratedCodeCompilationTests
{
    /// <summary>
    /// Both nullable contexts, because the generators emit differently for each: an NRT-off consumer gets
    /// <c>#nullable disable</c> headers and un-annotated DTO properties. The Disable branch had no coverage
    /// beyond a two-entity fixture whose references did not resolve, which is exactly the gap
    /// <c>docs/TODO.md</c> records — one entity SSOT compiled twice closes it without a second fixture to
    /// keep in step.
    /// </summary>
    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EveryDotNetGenerator_EmitsCodeThatCompiles(NullableContextOptions nullable)
    {
        Compilation withGenerated = GeneratedCodeCompilationHarness.CompileAllGenerators(
            out ImmutableArray<Diagnostic> generatorDiagnostics, nullable: nullable);

        AssertCompilesClean(withGenerated, generatorDiagnostics);
    }

    /// <summary>
    /// A <c>[ComplexManyToManyList]</c> over a KEYLESS <c>[M2M]</c> junction — the one collection control
    /// whose extracted entity IS the junction, so it is the shape that asks a keyless class for an id type.
    /// The entity SSOT could not catch this on its own: its only junction, <c>ProjectMember</c>, is
    /// <c>[M2M]</c> but <b>keyed</b> (<c>BusinessObject&lt;long&gt;</c>), payload-free, and reached by a
    /// MultiAutocomplete — so the keyless axis was structurally absent, and a release shipped that faulted
    /// six generators and left a consumer with ~1200 CS0246s.
    /// <para>
    /// Lives here rather than as a per-generator theory so it covers all ten generators with no
    /// hand-maintained list, and compiles the output instead of only asserting the absence of a fault. That
    /// is what caught the SECOND defect in this shape — the junction's own M2M service emitted
    /// <c>dto.FkId.Value</c> on a always-nullable DTO column, CS8629 under NRT-on.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void EveryDotNetGenerator_HandlesAComplexManyToManyListOverAKeylessJunction(NullableContextOptions nullable)
    {
        const string KeylessJunctionWithPayload = $$"""
            using System.Collections.Generic;
            using System.ComponentModel.DataAnnotations;
            using Spiderly.Shared.Attributes.Entity;
            using Spiderly.Shared.Attributes.Entity.UI;
            using Spiderly.Shared.BaseEntities;

            namespace {{GeneratedCodeCompilationHarness.AppName}}.Business.Entities
            {
                [SpiderlyEntity]
                public class Depot : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; } = null!;

                    [ComplexManyToManyList]
                    public virtual List<DepotArticle> DepotArticles { get; } = new();
                }

                [SpiderlyEntity]
                public class Article : BusinessObject<byte>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; } = null!;

                    public virtual List<DepotArticle> DepotArticles { get; } = new();
                }

                [M2M]
                [SpiderlyEntity]
                public class DepotArticle
                {
                    public long DepotId { get; set; }
                    [M2MWithMany(nameof(Depot.DepotArticles))]
                    public virtual Depot Depot { get; set; } = null!;

                    public byte ArticleId { get; set; }
                    [M2MWithMany(nameof(Article.DepotArticles))]
                    public virtual Article Article { get; set; } = null!;

                    public int Stock { get; set; }
                }
            }
            """;

        Compilation withGenerated = GeneratedCodeCompilationHarness.CompileAllGenerators(
            out ImmutableArray<Diagnostic> generatorDiagnostics, [KeylessJunctionWithPayload], nullable);

        AssertCompilesClean(withGenerated, generatorDiagnostics);
    }

    [Fact]
    public void NegativeControl_ABrokenEntityIsReported()
    {
        // Proves the harness is not a placebo. A detector whose only evidence is "it went red once, on an
        // unrelated pre-existing bug" is weakly validated; this validates it on every run. The entity below
        // references a type that does not exist, so the compilation MUST fail — if this test ever goes green,
        // the harness has stopped compiling anything and the test above is meaningless.
        const string BrokenEntity = $$"""
            namespace {{GeneratedCodeCompilationHarness.AppName}}.Business.Entities
            {
                [SpiderlyEntity]
                public class Deliberately : Spiderly.Shared.BaseEntities.BusinessObject<long>
                {
                    [System.ComponentModel.DataAnnotations.Required]
                    public ThisTypeDoesNotExist Broken { get; set; } = null!;
                }
            }
            """;

        Compilation withGenerated = GeneratedCodeCompilationHarness.CompileAllGenerators(out _, [BrokenEntity]);

        Assert.Contains(
            withGenerated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error),
            d => d.Id == "CS0246");
    }

    private static void AssertCompilesClean(Compilation withGenerated, ImmutableArray<Diagnostic> generatorDiagnostics)
    {
        // The GENERATORS' own diagnostics (SPIDERLY### and generator faults) are reported by the driver and
        // never reach the compilation, so they must be asserted separately. Omitting them is how a
        // SPIDERLY028 against the type-zoo fixture passed here and failed CI's real build.
        string[] generatorErrors = generatorDiagnostics
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .Select(Describe)
            .Distinct()
            .ToArray();

        Assert.True(
            generatorErrors.Length == 0,
            $"Generators reported {generatorErrors.Length} diagnostic(s):\n{string.Join("\n", generatorErrors)}");

        // Generated trees carry a generator-derived FilePath; trees parsed from a string have none. Without
        // this check "zero errors" would also pass for a harness that silently generated NOTHING, which is
        // the placebo the negative control alone does not rule out (its error comes from an input tree).
        HashSet<SyntaxTree> generatedTrees = withGenerated.SyntaxTrees
            .Where(t => !string.IsNullOrEmpty(t.FilePath))
            .ToHashSet();

        Assert.NotEmpty(generatedTrees);

        foreach (string expected in ExpectedGeneratedFiles)
        {
            Assert.Contains(generatedTrees, t => t.FilePath.EndsWith(expected));
        }

        List<Diagnostic> diagnostics = withGenerated.GetDiagnostics()
            .Where(d => d.Severity >= DiagnosticSeverity.Warning)
            .ToList();

        string[] errors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(Describe)
            .Distinct()
            .ToArray();

        Assert.True(errors.Length == 0, $"Generated code did not compile — {errors.Length} error(s):\n{string.Join("\n", errors)}");

        string[] generatedWarnings = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Warning)
            .Where(d => d.Location.SourceTree is not null && generatedTrees.Contains(d.Location.SourceTree))
            .Select(Describe)
            .Distinct()
            .ToArray();

        Assert.True(
            generatedWarnings.Length == 0,
            $"Generated code raised {generatedWarnings.Length} warning(s):\n{string.Join("\n", generatedWarnings)}");
    }

    /// <summary>
    /// One artifact per generator this compilation can reach, so a generator silently contributing nothing is
    /// a failure rather than just a smaller diagnostic count.
    /// <para>
    /// NOT here, and deliberately: <c>ControllerGenerator</c>. It returns early unless the calling project
    /// directory contains <c>.WebAPI</c>, and it reads entities/DTOs/services from REFERENCED assemblies
    /// rather than its own compilation. Controllers therefore belong to the multi-project axis —
    /// the one <c>tests/e2e-fixtures/CLAUDE.md</c> records as structurally unreachable from a single
    /// compilation, and the one that produced a real CS1061 when two generators disagreed about a flag set
    /// only on current-project classes. Until that variant exists here, a type error in a generated
    /// controller is still e2e-only.
    /// </para>
    /// </summary>
    private static readonly string[] ExpectedGeneratedFiles =
    [
        "DTOList.generated.cs",
        "Mapper.generated.cs",
        "EntityServiceDependencies.generated.cs",
        "EntityServiceRegistration.generated.cs",
        "TaskCommentService.generated.cs",
        "ValidationRules.generated.cs",
        "PermissionCodes.generated.cs",
        "PaginatedResultGenerator.generated.cs",
        "ExcelPropertiesToExclude.generated.cs",
        "AuthorizationService.generated.cs",
    ];

    private static string Describe(Diagnostic diagnostic)
    {
        FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
        string file = string.IsNullOrEmpty(span.Path) ? "<generated>" : span.Path;

        return $"{diagnostic.Id} {file}{span.StartLinePosition}: {diagnostic.GetMessage()}";
    }
}
