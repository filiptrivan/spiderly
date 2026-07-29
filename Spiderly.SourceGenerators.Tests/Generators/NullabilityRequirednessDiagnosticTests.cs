using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Under <c>&lt;Nullable&gt;enable&lt;/Nullable&gt;</c> an entity property's NRT annotation IS its column's
/// nullability: nothing configures scalar requiredness, and <c>ConfigureManyToOneRelationships</c> hands EF
/// the navigation, whose nullability EF reads as the relationship's requiredness. So an annotation that
/// disagrees with <c>[Required]</c> silently rewrites the schema — the next migration alters columns to
/// NOT NULL, and a save that legitimately omits the value writes a default instead of NULL.
/// <para>
/// It shipped twice. Annotating the e2e fixture's <c>TaskComment.Category</c> (no <c>[Required]</c>)
/// non-nullable made every comment insert die on
/// <c>23503 violates foreign key constraint "FK_TaskComment_TaskCategory_CategoryId"</c>, and an audit
/// found three more latent instances that were only invisible because every test supplied the FK.
/// </para>
/// <para>
/// Only meaningful under an annotated compilation: a nullable-oblivious consumer writes
/// <c>public virtual Category Category</c> with no <c>?</c>, and that is the ABSENCE of an annotation, not
/// a claim of non-nullability. The Disable cases below are the guard on that.
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

    private static bool Emits(string members, NullableContextOptions nullable)
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
        """, NullableContextOptions.Enable));
    }

    [Fact]
    public void RequiredNavigation_AnnotatedNullable_IsReported()
    {
        Assert.True(Emits("""
                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category? Category { get; set; }
        """, NullableContextOptions.Enable));
    }

    [Fact]
    public void AgreeingNavigations_AreNotReported()
    {
        Assert.False(Emits("""
                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; } = null!;
        """, NullableContextOptions.Enable));

        Assert.False(Emits("""
                [WithMany(nameof(Category.Products))]
                public virtual Category? Category { get; set; }
        """, NullableContextOptions.Enable));
    }

    // --- Scalars: nothing configures their requiredness either, so the same rule applies ---

    [Fact]
    public void OptionalScalar_AnnotatedNonNullable_IsReported()
    {
        Assert.True(Emits("""
                public string Slug { get; set; } = null!;
        """, NullableContextOptions.Enable));
    }

    [Fact]
    public void RequiredScalar_AnnotatedNullable_IsReported()
    {
        Assert.True(Emits("""
                [Required]
                public string? Slug { get; set; }
        """, NullableContextOptions.Enable));
    }

    [Fact]
    public void ValueTypeScalars_AreNotReported()
    {
        // A value type carries its own nullability in the CLR type; [Required] adds nothing EF doesn't
        // already know, so the two cannot disagree.
        Assert.False(Emits("""
                public int Stock { get; set; }
        """, NullableContextOptions.Enable));
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
