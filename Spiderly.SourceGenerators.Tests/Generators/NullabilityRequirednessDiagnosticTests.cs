using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// SPIDERLY028 — see <c>SpiderlyDiagnostics.NullabilityRequirednessMismatch</c> for why a disagreement
/// between the annotation and <c>[Required]</c> matters.
/// <para>
/// Why it earns tests: it shipped. Annotating the e2e fixture's <c>TaskComment.Category</c> (no
/// <c>[Required]</c>) non-nullable made every comment insert die on <c>23503 violates foreign key
/// constraint "FK_TaskComment_TaskCategory_CategoryId"</c>, and an audit found three more latent
/// instances, invisible only because every test happened to supply the FK.
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
        var driver = GeneratorTestHarness.Run<MapperGenerator>(EntityWith(members), nullable);

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
