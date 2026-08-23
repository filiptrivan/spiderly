using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// Negative controls for the SPIDERLY030 detector: blob key prefixes are the listing scope for
/// cleanup and staging promotion, so two properties sharing an effective prefix would delete each
/// other's objects. The validator must reject duplicate effective prefixes (custom or default),
/// path-parent collisions, and non-key-safe custom prefixes — and stay silent on a clean model.
/// </summary>
public class BlobKeyPrefixValidatorTests
{
    private static SpiderlyClass MakeEntity(string name, params SpiderlyProperty[] blobProperties)
    {
        SpiderlyClass entity = new()
        {
            Name = name,
            Namespace = "TestApp.Entities",
            BaseType = "BusinessObject<long>",
        };

        foreach (SpiderlyProperty property in blobProperties)
        {
            property.EntityName = name;
            entity.Properties.Add(property);
        }

        return entity;
    }

    private static SpiderlyProperty BlobProperty(string name, string? keyPrefix = null)
    {
        SpiderlyProperty property = new()
        {
            Name = name,
            Type = "string",
        };

        property.Attributes.Add(new SpiderlyAttribute
        {
            Name = "S3PublicStorage",
            Value = keyPrefix == null ? null : $"KeyPrefix = {keyPrefix}",
        });

        return property;
    }

    [Fact]
    public void CleanModel_DistinctPrefixes_Passes()
    {
        List<SpiderlyClass> entities =
        [
            MakeEntity("ProductMedia", BlobProperty("Url", "products"), BlobProperty("ThumbnailUrl", "products-thumb")),
            MakeEntity("Brand", BlobProperty("Image")), // default Brand/Image
        ];

        BlobKeyPrefixValidator.Validate(entities); // must not throw
    }

    [Fact]
    public void DuplicateCustomPrefix_ThrowsSPIDERLY030()
    {
        List<SpiderlyClass> entities =
        [
            MakeEntity("ProductMedia", BlobProperty("Url", "products")),
            MakeEntity("Catalog", BlobProperty("File", "products")),
        ];

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => BlobKeyPrefixValidator.Validate(entities));

        Assert.Equal("SPIDERLY030", ex.Diagnostic.Id);
        Assert.Contains("products", ex.Diagnostic.GetMessage());
    }

    [Fact]
    public void CustomPrefixShadowingAnotherPropertysDefault_ThrowsSPIDERLY030()
    {
        List<SpiderlyClass> entities =
        [
            MakeEntity("Brand", BlobProperty("Image")), // default Brand/Image
            MakeEntity("Catalog", BlobProperty("File", "Brand/Image")),
        ];

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => BlobKeyPrefixValidator.Validate(entities));

        Assert.Equal("SPIDERLY030", ex.Diagnostic.Id);
    }

    [Fact]
    public void PathParentPrefix_ThrowsSPIDERLY030()
    {
        // "products" listing/staging scope sits above "products/extra" keys — reject the nesting.
        List<SpiderlyClass> entities =
        [
            MakeEntity("ProductMedia", BlobProperty("Url", "products")),
            MakeEntity("Catalog", BlobProperty("File", "products/extra")),
        ];

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => BlobKeyPrefixValidator.Validate(entities));

        Assert.Equal("SPIDERLY030", ex.Diagnostic.Id);
    }

    [Theory]
    [InlineData("Products")] // uppercase
    [InlineData("proizvodi ba")] // whitespace
    [InlineData("šrafovi")] // non-ASCII — keys are public URLs, they must not percent-encode
    [InlineData("products/")] // trailing slash
    [InlineData("products/_tmp")] // collides with the staging segment
    [InlineData("")] // explicit empty
    public void MalformedCustomPrefix_ThrowsSPIDERLY030(string keyPrefix)
    {
        List<SpiderlyClass> entities =
        [
            MakeEntity("ProductMedia", BlobProperty("Url", keyPrefix)),
        ];

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => BlobKeyPrefixValidator.Validate(entities));

        Assert.Equal("SPIDERLY030", ex.Diagnostic.Id);
    }
}
