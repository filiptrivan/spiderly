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

                public virtual List<Product> SecondaryProducts { get; } = new();
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

                [WithMany(nameof(Category.SecondaryProducts))]
                public virtual Category SecondaryCategory { get; set; }

                public virtual List<Review> Reviews { get; } = new();
            }

            // The init template's UserExternalLogin shape: an explicit FK scalar beside a [Required]
            // nav. No {Nav}Id is synthesized here — the scalar flows through the scalar branch under
            // its OWN requiredness, which is not the nav's.
            [SpiderlyEntity]
            public class Review : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Title { get; set; }

                public long ProductId { get; set; }

                [Required]
                [WithMany(nameof(Product.Reviews))]
                public virtual Product Product { get; set; }
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

    // --- Value types stay nullable, [Required] or not ---

    // Tried the other way and reverted: a generated {Entity}DTO is also the Angular form model (an
    // empty numeric input posts null) and the sparse placeholder carrier for [ComplexManyToManyList]
    // grids, where an all-null row IS the "no record" sentinel. 'int' can't hold null, so both
    // protocols break. Reference types are safe to tighten because, with RespectNullableAnnotations
    // off, a non-nullable 'string' still accepts null at runtime.
    [Theory]
    [InlineData(NullableContextOptions.Disable)]
    [InlineData(NullableContextOptions.Enable)]
    public void RequiredValueType_StaysNullable(NullableContextOptions nullable)
    {
        Assert.Contains("public int? Quantity { get; set; }", RunDtoGenerator(nullable));
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
    public void RequiredNav_StillFlattensToANullableForeignKey(NullableContextOptions nullable)
    {
        // A flattened FK is a value type, so it follows the rule above. It is also exactly where
        // tightening hurt most: the ComplexManyToManyList placeholder rows carry null FKs by design.
        Assert.Contains("public long? CategoryId { get; set; }", RunDtoGenerator(nullable));
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

    // --- Sibling generators must read the flattened FK at its ACTUAL nullability ---

    // Both FK shapes unwrap, because both columns are nullable — but the emitters must DERIVE that
    // from the column rather than assume it, which is what GetDTOForeignKeyAccessExpression does.
    // These pin the derivation so a future tightening can't silently emit '.Value' on a bare value
    // type (CS1061) or omit it on a Nullable<T> (CS1503) — both of which reached CI once.
    [Fact]
    public void SaveService_UnwrapsASynthesizedForeignKey()
    {
        string services = RunServicesGenerator();

        Assert.Contains("dto.CategoryId.Value", services);
        Assert.Contains("dto.SecondaryCategoryId.Value", services);
    }

    // An explicit FK suppresses {Nav}Id synthesis, so the column is the SCALAR, under its own name and
    // its own nullability — not the navigation's. This is the shape the init template's
    // UserExternalLogin ships, so every scaffolded app exercises it.
    [Fact]
    public void SaveService_ReadsAnExplicitForeignKeyAtTheScalarsNullability()
    {
        Assert.Contains("dto.ProductId.Value", RunServicesGenerator("ReviewService.generated.cs"));
    }

    private static string RunServicesGenerator(string hintName = "ProductService.generated.cs")
    {
        var driver = GeneratorTestHarness.Run<ServicesGenerator>(Source);

        return driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName == hintName).SourceText.ToString();
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
