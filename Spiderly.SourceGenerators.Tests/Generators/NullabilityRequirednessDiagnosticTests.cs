using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// SPIDERLY028 — see <c>SpiderlyDiagnostics.NullabilityRequirednessMismatch</c> for why a disagreement
/// between the annotation and <c>[Required]</c> matters.
/// <para>
/// Why it earns tests: the disagreement it rejects shipped four times. The instance that surfaced
/// (<c>TaskComment.Category</c> — no <c>[Required]</c>, annotated non-nullable) made every comment insert
/// die on <c>23503 violates foreign key constraint "FK_TaskComment_TaskCategory_CategoryId"</c>, and an
/// audit found three more latent ones, invisible only because every test happened to supply the FK.
/// </para>
/// <para>
/// That foreign-key violation's actual cause was an ordering bug in <c>ConfigureManyToOneRelationships</c>
/// (<c>.IsRequired()</c> called before <c>.HasForeignKey()</c>), since fixed and pinned by
/// <c>Spiderly.Infrastructure.Tests.RequirednessColumnNullabilityTests</c> — so this diagnostic is NOT what
/// stands between a consumer and that schema bug, and must not be described as if it were. It earns its
/// keep on the two costs in the descriptor: a lying NAVIGATION annotation still hands the consumer a
/// <c>null</c> materialized into a non-nullable property, and a lying SCALAR annotation still alters the
/// column.
/// </para>
/// <para>
/// The Disable case is the load-bearing guard: a nullable-oblivious consumer writes
/// <c>public virtual Category Category</c> with no <c>?</c>, which is the ABSENCE of an annotation rather
/// than a claim of non-nullability. Reporting there would break every such consumer's build.
/// </para>
/// </summary>
public class NullabilityRequirednessDiagnosticTests
{
    private const string Id = "SPIDERLY028";

    private static string EntityWith(string members) => $$"""
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; } = null!;

                public virtual List<Product> Products { get; } = new();
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Title { get; set; } = null!;

        {{members}}
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }
        """;

    private static bool Emits(string members, NullableContextOptions nullable = NullableContextOptions.Enable)
    {
        var driver = GeneratorTestHarness.Run<EntityValidationGenerator>(EntityWith(members), nullable);

        return driver.GetRunResult().Diagnostics.Any(d => d.Id == Id);
    }

    // --- Navigations ---

    [Fact]
    public void OptionalNavigation_AnnotatedNonNullable_IsReported()
    {
        Assert.True(Emits("""
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; } = null!;
        """));
    }

    [Fact]
    public void RequiredNavigation_AnnotatedNullable_IsReported()
    {
        Assert.True(Emits("""
                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category? Category { get; set; }
        """));
    }

    [Fact]
    public void AgreeingRequiredNavigation_IsNotReported()
    {
        Assert.False(Emits("""
                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; } = null!;
        """));
    }

    [Fact]
    public void AgreeingOptionalNavigation_IsNotReported()
    {
        Assert.False(Emits("""
                [WithMany(nameof(Category.Products))]
                public virtual Category? Category { get; set; }
        """));
    }

    // --- Scalars: Spiderly configures no scalar requiredness, so EF's conventions decide (and disagree by
    // direction — see the descriptor). Same rule either way: the two must agree. ---

    [Fact]
    public void OptionalScalar_AnnotatedNonNullable_IsReported()
    {
        Assert.True(Emits("""
                public string Slug { get; set; } = null!;
        """));
    }

    [Fact]
    public void RequiredScalar_AnnotatedNullable_IsReported()
    {
        Assert.True(Emits("""
                [Required]
                public string? Slug { get; set; }
        """));
    }

    [Fact]
    public void ValueTypeScalars_AreNotReported()
    {
        // A value type carries its own nullability in the CLR type; [Required] adds nothing EF doesn't
        // already know, so the two cannot disagree.
        Assert.False(Emits("""
                public int Stock { get; set; }
        """));
    }

    // --- Not switchable off: the reason these live in EntityValidationGenerator ---

    [Fact]
    public void Reported_EvenWhenEveryArtifactGeneratorIsDisabled()
    {
        // The hole this move closed. While these validators lived in MapperGenerator, one line in
        // .spiderly/config.json silently disabled every entity diagnostic Spiderly has. Declining a
        // generated ARTIFACT must not decline the checks that say the entities are well-formed.
        const string AllDisabled = """
            {
              "generators": {
                "MapperGenerator": false,
                "EntitiesToDTOGenerator": false,
                "ServicesGenerator": false,
                "ControllerGenerator": false,
                "FluentValidationGenerator": false
              }
            }
            """;

        var driver = GeneratorTestHarness.Run<EntityValidationGenerator>(
            EntityWith("""
                    [WithMany(nameof(Category.Products))]
                    public virtual Category Category { get; set; } = null!;
            """),
            NullableContextOptions.Enable,
            spiderlyConfigJson: AllDisabled);

        Assert.Contains(driver.GetRunResult().Diagnostics, d => d.Id == Id);
    }

    // --- The oblivious guard: no annotations exist, so nothing can disagree ---

    [Fact]
    public void ObliviousConsumer_IsNeverReported()
    {
        Assert.False(Emits("""
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; }

                public string Slug { get; set; }
        """, NullableContextOptions.Disable));
    }
}
