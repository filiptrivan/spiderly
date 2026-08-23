using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Pins the blob-key naming seam in generated entity services:
/// <list type="bullet">
/// <item><c>KeyPrefix</c> on a storage attribute replaces the default
/// <c>{Entity}/{Property}</c> key prefix at every storage call site (upload, cleanup,
/// staged-blob promotion, editor images).</item>
/// <item>Every blob/editor property gets an overridable
/// <c>GetBlobDescriptiveNameFor{Prop}Of{Entity}</c> hook, called eagerly on direct upload
/// (id &gt; 0) and lazily at staged-blob promotion.</item>
/// <item>The upload file name is re-aligned with the optimized bytes
/// (<c>Helper.AlignExtensionWithContent</c>) so the key extension and Content-Type describe
/// what is actually stored, not what the admin picked.</item>
/// </list>
/// </summary>
public class BlobKeyNamingTests
{
    [Fact]
    public Task BlobAndEditorProperties_CustomAndDefaultKeyPrefixes_EmitNamingSeam()
    {
        const string source = """
            using System.Collections.Generic;

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

                    [S3PublicStorage]
                    [AcceptedFileTypes("image/*")]
                    [StringLength(1000, MinimumLength = 1)]
                    public string ThumbnailUrl { get; set; }

                    [UIControlType(nameof(UIControlTypeCodes.Editor))]
                    [S3PublicStorage(KeyPrefix = "products-description")]
                    [AcceptedFileTypes("image/*")]
                    public string HtmlDescription { get; set; }
                }
            }

            namespace TestApp.Business.Services
            {
                [SpiderlyService]
                public class ProductMediaService : ProductMediaServiceGenerated { }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }

    [Fact]
    public Task RetainReplacedBlobs_SuppressesSaveTimeCleanupForThatPropertyOnly()
    {
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Banner : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    [S3PublicStorage]
                    [RetainReplacedBlobs]
                    [AcceptedFileTypes("image/*")]
                    [StringLength(1000, MinimumLength = 1)]
                    public string SnapshottedUrl { get; set; }

                    [S3PublicStorage]
                    [AcceptedFileTypes("image/*")]
                    [StringLength(1000, MinimumLength = 1)]
                    public string LogoUrl { get; set; }
                }
            }

            namespace TestApp.Business.Services
            {
                [SpiderlyService]
                public class BannerService : BannerServiceGenerated { }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }
}
