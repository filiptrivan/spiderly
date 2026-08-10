using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;
using Xunit;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Pins which read shapes populate a blob property's <c>{Property}Data</c>. The rationale for the
/// split lives on <c>ServiceReadGenerator.GetPopulateDTOWithBlobPartsForDTOList</c>.
/// </summary>
public class BlobDataPopulationTests
{
    private const string Source = """
        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Receipt : BusinessObject<long>
            {
                [DisplayName]
                public string Title { get; set; }

                [S3PrivateStorage]
                [AcceptedFileTypes("image/png")]
                public string ScanUrl { get; set; }

                [S3PublicStorage]
                [AcceptedFileTypes("image/png")]
                public string ThumbnailUrl { get; set; }
            }
        }
        """;

    private static readonly Lazy<SyntaxTree> GeneratedTree = new(() =>
        GeneratorTestHarness.Run<ServicesGenerator>(Source).GetRunResult().GeneratedTrees
            .Single(t => t.FilePath.EndsWith("ReceiptService.generated.cs")));

    /// <summary>
    /// One read method, selected by name — <c>Single</c> fails loudly if the generator renames it,
    /// rather than silently yielding an empty body that every assertion would vacuously pass.
    /// </summary>
    private static string ReadMethod(string name)
    {
        return GeneratedTree.Value.GetRoot().DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(m => m.Identifier.Text == name)
            .ToString();
    }

    [Fact]
    public void Single_row_read_loads_private_blob_data()
    {
        Assert.Contains(
            "dto.ScanUrlData = await _s3PrivateStorageService.GetFileDataAsync(dto.ScanUrl);",
            ReadMethod("GetReceiptDTO"));
    }

    [Theory]
    [InlineData("GetPaginatedReceiptList")]
    [InlineData("GetReceiptDTOList")]
    public void List_reads_do_not_load_private_blob_data(string method)
    {
        Assert.DoesNotContain("GetFileDataAsync", ReadMethod(method));
    }

    [Theory]
    [InlineData("GetPaginatedReceiptList")]
    [InlineData("GetReceiptDTOList")]
    public void List_reads_still_pass_through_public_blob_urls(string method)
    {
        Assert.Contains("dto.ThumbnailUrlData = dto.ThumbnailUrl;", ReadMethod(method));
    }
}
