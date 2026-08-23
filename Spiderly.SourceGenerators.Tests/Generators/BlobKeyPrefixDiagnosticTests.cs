using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using System.Collections.Immutable;
using System.Linq;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// SPIDERLY030 at the pipeline level. <see cref="BlobKeyPrefixValidatorTests"/> covers the rule
/// itself; this covers where it is HOSTED, which is the part that regressed once already: the
/// validator was first called from <see cref="ServicesGenerator"/>, where
/// <c>IsGeneratorEnabled</c> could silently switch the check off and the throw aborted the whole
/// Execute — no entity services emitted, the located error buried under cascading CS0246s. Same
/// failure <see cref="SpiderlyEntityValidator"/>'s docblock records for SPIDERLY029.
/// </summary>
public class BlobKeyPrefixDiagnosticTests
{
    private const string CollidingPrefixes = """
        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class ProductMedia : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                [S3PublicStorage(KeyPrefix = "products")]
                [AcceptedFileTypes("image/*")]
                [StringLength(1000, MinimumLength = 1)]
                public string Url { get; set; }
            }

            [SpiderlyEntity]
            public class Catalog : BusinessObject<int>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }

                [S3PublicStorage(KeyPrefix = "products")]
                [AcceptedFileTypes("application/pdf")]
                [StringLength(1000, MinimumLength = 1)]
                public string File { get; set; }
            }
        }
        """;

    [Fact]
    public void CollidingKeyPrefixes_EmitSPIDERLY030AtTheProperty()
    {
        ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.Run<EntityValidationGenerator>(CollidingPrefixes)
            .GetRunResult().Diagnostics;

        Diagnostic diagnostic = Assert.Single(diagnostics);

        Assert.Equal("SPIDERLY030", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("products", diagnostic.GetMessage());
        Assert.NotEqual(Location.None, diagnostic.Location);
    }

    [Fact]
    public void CollidingKeyPrefixes_DoNotSuppressTheGeneratedServices()
    {
        // The reason the check is not hosted in ServicesGenerator: a bad prefix must cost the
        // consumer one located error, not every *ServiceGenerated class in the compilation.
        var result = GeneratorTestHarness.Run<ServicesGenerator>(CollidingPrefixes).GetRunResult();

        Assert.Contains(result.GeneratedTrees, t => t.FilePath.Contains("ProductMediaService"));
        Assert.Contains(result.GeneratedTrees, t => t.FilePath.Contains("CatalogService"));
    }

    [Fact]
    public void DistinctKeyPrefixes_AreNotReported()
    {
        // Negative control: a validator that fired unconditionally would pass the test above.
        const string distinctPrefixes = """
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class ProductMedia : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    [S3PublicStorage(KeyPrefix = "products")]
                    [AcceptedFileTypes("image/*")]
                    [StringLength(1000, MinimumLength = 1)]
                    public string Url { get; set; }
                }

                [SpiderlyEntity]
                public class Catalog : BusinessObject<int>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    [S3PublicStorage(KeyPrefix = "catalogs")]
                    [AcceptedFileTypes("application/pdf")]
                    [StringLength(1000, MinimumLength = 1)]
                    public string File { get; set; }
                }
            }
            """;

        ImmutableArray<Diagnostic> diagnostics = GeneratorTestHarness.Run<EntityValidationGenerator>(distinctPrefixes)
            .GetRunResult().Diagnostics;

        Assert.Empty(diagnostics);
    }
}
