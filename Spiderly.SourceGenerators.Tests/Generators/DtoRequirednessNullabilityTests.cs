using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Generated DTO nullability is keyed off <c>[Required]</c>, not off "everything is optional on the
/// wire" and not off the entity's NRT annotation. Required -&gt; non-nullable (reference types carry
/// <c>= null!</c>); optional -&gt; nullable. The value-type half is expressed in the type itself
/// (<c>int?</c> IS <c>Nullable&lt;T&gt;</c>), so it reaches nullable-oblivious consumers too; the
/// reference-type half is an annotation and only appears under an annotated context.
/// <para>
/// Keying off <c>[Required]</c> rather than the entity's <c>string?</c> is what keeps
/// <see cref="NullableAnnotatedEntityTests"/>'s oblivious-parity guarantee meaningful: an NRT-off
/// consumer has no annotation to read, so the attribute is the only signal available in both
/// contexts — and it is already the one EF (NOT NULL), Swashbuckle, and FluentValidation read.
/// </para>
/// </summary>
public class DtoRequirednessNullabilityTests
{
    private const string Source = """
        using System.Collections.Generic;

        namespace TestApp.Business.Enums
        {
            [SpiderlyEnum]
            public enum StatusCodes { Active = 1, Archived = 2 }
        }

        namespace TestApp.Business.Entities
        {
            using TestApp.Business.Enums;

            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                public virtual List<Product> Products { get; } = new();
            }

            [SpiderlyEntity]
            public class Tag : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Title { get; set; }

                // Lands in the SaveBody DTO as List<long> SelectedTagsIds — the only DTO column
                // shape that carries an = new() initializer.
                [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
                public virtual List<Tag> Tags { get; } = new();

                public string Description { get; set; }

                [Required]
                public int Quantity { get; set; }

                public int? Stock { get; set; }

                [Required]
                public StatusCodes Status { get; set; }

                [Required]
                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; }
            }
        }
        """;

    private static string RunDtoGenerator(NullableContextOptions nullable)
    {
        var driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(Source, nullable);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName == "DTOList.generated.cs").SourceText.ToString();
    }

    // --- Reference types: annotation-only, so the oblivious branch must stay bare ---

    [Fact]
    public void RequiredString_IsNonNullableWithNullForgivingInitializer_UnderNrt()
    {
        Assert.Contains("public string Title { get; set; } = null!;", RunDtoGenerator(NullableContextOptions.Enable));
    }

    [Fact]
    public void OptionalString_IsNullable_UnderNrt()
    {
        string dtos = RunDtoGenerator(NullableContextOptions.Enable);

        Assert.Contains("public string? Description { get; set; }", dtos);
        // The initializer is what makes a non-nullable property nullable-clean; an optional one must
        // not carry it, or the '?' would be asserting and denying the same thing.
        Assert.DoesNotContain("public string? Description { get; set; } = null!;", dtos);
    }

    [Fact]
    public void ReferenceTypes_StayBare_ForAnObliviousConsumer()
    {
        string dtos = RunDtoGenerator(NullableContextOptions.Disable);

        Assert.Contains("public string Title { get; set; }", dtos);
        Assert.Contains("public string Description { get; set; }", dtos);
        // '?' on a reference type is a CS8632 in an oblivious consumer, and '= null!' is noise there.
        Assert.DoesNotContain("public string? ", dtos);
        Assert.DoesNotContain("= null!;", dtos);
    }

    // --- Value types: 'int?' IS the type, so requiredness reaches BOTH contexts ---

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void RequiredValueType_IsNonNullable(NullableContextOptions nullable)
    {
        string dtos = RunDtoGenerator(nullable);

        Assert.Contains("public int Quantity { get; set; }", dtos);
        Assert.DoesNotContain("public int? Quantity { get; set; }", dtos);
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void OptionalValueType_IsNullable(NullableContextOptions nullable)
    {
        Assert.Contains("public int? Stock { get; set; }", RunDtoGenerator(nullable));
    }

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void RequiredEnum_IsNonNullable(NullableContextOptions nullable)
    {
        string dtos = RunDtoGenerator(nullable);

        Assert.Contains("public StatusCodes Status { get; set; }", dtos);
        Assert.DoesNotContain("public StatusCodes? Status { get; set; }", dtos);
    }

    // --- Derived columns (GetDTOColumns) ---

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void RequiredNav_FlattensToANonNullableForeignKey(NullableContextOptions nullable)
    {
        string dtos = RunDtoGenerator(nullable);

        // ForeignKeyValidator already fails the build when nav-requiredness and FK-nullability
        // disagree, so [Required] on the nav IS a declared fact about this entity's FK column.
        Assert.Contains("public long CategoryId { get; set; }", dtos);
        Assert.DoesNotContain("public long? CategoryId { get; set; }", dtos);
    }

    [Fact]
    public void NavDisplayName_StaysNullable_EvenForARequiredNav()
    {
        // Deliberately NOT derived from the target entity's [DisplayName] requiredness: that would
        // make adding [Required] to Category.Name a silent wire-contract change to ProductDTO, with
        // no signal at the edit site. Accepted cost: CategoryId non-nullable beside
        // CategoryDisplayName? on the same flattened nav.
        string dtos = RunDtoGenerator(NullableContextOptions.Enable);

        Assert.Contains("public string? CategoryDisplayName { get; set; }", dtos);
    }

    // --- Collections keep their = new() exemption ---

    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void Collections_KeepTheirEmptyInstanceInitializer(NullableContextOptions nullable)
    {
        string dtos = RunDtoGenerator(nullable);

        Assert.Contains("= new();", dtos);
        Assert.DoesNotContain("List<long>? ", dtos);
    }
}
