using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// Negative controls for the blob key-prefix detector. The rule it enforces is deliberately split
/// in two, and the split is the point: <b>SPIDERLY030 (Error)</b> is about losing files — prefixes
/// are the listing scope for cleanup and staging promotion, so a collision deletes another
/// property's objects and an unusable prefix breaks the mechanism. <b>SPIDERLY031 (Warning)</b> is
/// only house style, so a consumer with an existing bucket layout can suppress it and keep the
/// real guard armed.
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

    private static List<string> Validate(params SpiderlyClass[] entities) =>
        BlobKeyPrefixValidator.Validate(entities.ToList()).Select(d => d.Id).ToList();

    [Fact]
    public void CleanModel_DistinctPrefixes_Passes()
    {
        Assert.Empty(Validate(
            MakeEntity("ProductMedia", BlobProperty("Url", "products"), BlobProperty("ThumbnailUrl", "products-thumb")),
            MakeEntity("Brand", BlobProperty("Image")))); // default Brand/Image
    }

    [Fact]
    public void DuplicateCustomPrefix_IsAnError()
    {
        List<string> ids = Validate(
            MakeEntity("ProductMedia", BlobProperty("Url", "products")),
            MakeEntity("Catalog", BlobProperty("File", "products")));

        Assert.Equal(["SPIDERLY030"], ids);
    }

    [Fact]
    public void CustomPrefixShadowingAnotherPropertysDefault_IsAnError()
    {
        List<string> ids = Validate(
            MakeEntity("Brand", BlobProperty("Image")), // default Brand/Image
            MakeEntity("Catalog", BlobProperty("File", "Brand/Image")));

        Assert.Contains("SPIDERLY030", ids);
    }

    [Fact]
    public void PathParentPrefix_IsAnError()
    {
        // "products" listing/staging scope sits above "products/extra" keys — reject the nesting.
        List<string> ids = Validate(
            MakeEntity("ProductMedia", BlobProperty("Url", "products")),
            MakeEntity("Catalog", BlobProperty("File", "products/extra")));

        Assert.Contains("SPIDERLY030", ids);
    }

    [Theory]
    [InlineData("proizvodi ba")] // whitespace
    [InlineData("šrafovi")] // non-ASCII — keys are public URLs, they must not percent-encode
    [InlineData("products/")] // trailing slash = empty segment
    [InlineData("/products")] // leading slash
    [InlineData("products//thumbs")] // doubled slash
    [InlineData("")] // explicit empty
    public void PrefixThatBreaksTheMechanism_IsAnError(string keyPrefix)
    {
        List<string> ids = Validate(MakeEntity("ProductMedia", BlobProperty("Url", keyPrefix)));

        Assert.Equal(["SPIDERLY030"], ids);
    }

    [Fact]
    public void PrefixUsingTheReservedStagingSegment_IsAnError()
    {
        // A permanent blob under a "_tmp" segment reads as a not-yet-promoted upload.
        List<string> ids = Validate(MakeEntity("ProductMedia", BlobProperty("Url", "products/_tmp")));

        Assert.Equal(["SPIDERLY030"], ids);
    }

    [Theory]
    [InlineData("Products")] // uppercase
    [InlineData("product_images")] // underscores
    public void PrefixThatMerelyDepartsFromHouseStyle_IsOnlyAWarning(string keyPrefix)
    {
        List<Diagnostic> diagnostics = BlobKeyPrefixValidator.Validate(
            [MakeEntity("ProductMedia", BlobProperty("Url", keyPrefix))]);

        Diagnostic diagnostic = Assert.Single(diagnostics);

        // These are legal keys in every provider Spiderly targets. Failing the build over them
        // would be a house preference blocking a consumer with an existing bucket layout.
        Assert.Equal("SPIDERLY031", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void SuppressingTheStyleWarningLeavesTheCollisionErrorArmed()
    {
        // The reason the two have separate ids: a consumer who suppresses SPIDERLY031 to keep
        // their own naming must still be told when one property will delete another's blobs.
        List<string> ids = Validate(
            MakeEntity("ProductMedia", BlobProperty("Url", "Products")),
            MakeEntity("Catalog", BlobProperty("File", "Products")));

        Assert.Contains("SPIDERLY030", ids);
        Assert.Contains("SPIDERLY031", ids);
    }
}
