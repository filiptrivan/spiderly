using System.IO;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// The Angular generators write their <c>.generated.ts</c> files to disk, under the consumer's
/// <c>Frontend</c> folder. So each test gives the generator a temporary app folder and reads back the file.
/// </summary>
public sealed class GeneratedTypeScriptFileTests : IDisposable
{
    // The calling project is a WebAPI project, as NgControllersGenerator and NgValidatorsGenerator require.
    // NgValidatorsGenerator reads the referenced Spiderly.Security DTOs.
    private const string AppSource = """
        using System;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Brand : BusinessObject<long>
            {
                [DisplayName]
                [Required]
                public string Name { get; set; }
            }
        }

        namespace TestApp.Business.Enums
        {
            [SpiderlyEnum]
            public enum BrandStatusCodes
            {
                Active = 1,
            }
        }

        namespace TestApp.WebAPI.Controllers
        {
            [SpiderlyController]
            public class ReportController
            {
                [HttpGet]
                public int GetOrderCount(long brandId, DateTime from, DateTime? to) => 0;
            }
        }
        """;

    private readonly string _appFolder = Path.Combine(Path.GetTempPath(), $"spiderly-ts-{Guid.NewGuid():N}");

    private string ProjectDir => Path.Combine(_appFolder, "Backend", "TestApp.WebAPI");

    private string BusinessFolder => Path.Combine(_appFolder, "Frontend", "src", "app", "business");

    public GeneratedTypeScriptFileTests()
    {
        Directory.CreateDirectory(ProjectDir);

        // A generator writes into its folder but never creates it; `spiderly init` does.
        foreach (string folder in new[] { "components", "entities", "enums", Path.Combine("services", "api"), Path.Combine("services", "validators") })
            Directory.CreateDirectory(Path.Combine(BusinessFolder, folder));
    }

    public void Dispose() => Directory.Delete(_appFolder, recursive: true);

    [Theory]
    [InlineData(typeof(NgEntitiesGenerator), "entities/entities.generated.ts", "the [SpiderlyEntity] and [SpiderlyDTO] classes")]
    [InlineData(typeof(NgEnumsGenerator), "enums/enums.generated.ts", "the [SpiderlyEnum] enums and classes, and the [SpiderlyEntity] classes for PermissionCodes")]
    [InlineData(typeof(NgBaseDetailsGenerator), "components/base-details.generated.ts", "the [SpiderlyEntity] and [SpiderlyDTO] classes and their UI attributes")]
    [InlineData(typeof(NgValidatorsGenerator), "services/validators/validators.generated.ts", "the validation attributes on the [SpiderlyEntity] and [SpiderlyDTO] properties")]
    [InlineData(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", "the [SpiderlyController] classes, and the [SpiderlyEntity] classes for the CRUD methods")]
    public void GeneratedFile_OpensWithAHeaderThatNamesItsBackendSource(Type generatorType, string file, string backendSource)
    {
        string text = Generate(generatorType, file);

        Assert.StartsWith(
            $"// Spiderly's {generatorType.Name} writes this file. Do not edit it, because the next backend build overwrites it.\n" +
            $"// To change this file, change its backend source: {backendSource}.\n",
            text);
    }

    // Why a Date goes in its ISO form, and a null Date as it does: NgControllersGenerator.GetQueryParamValue.
    [Fact]
    public void ApiService_DateTimeQueryParam_IsSentAsAnIsoString()
    {
        string text = Generate(typeof(NgControllersGenerator), "services/api/api.service.generated.ts");

        Assert.Contains("getOrderCount = (brandId: number, from: Date, to: Date): Observable<number>", text);
        Assert.Contains("let params = `?brandId=${brandId}&from=${from?.toISOString()}&to=${to?.toISOString()}`;", text);
    }

    [Theory]
    [InlineData("52_428_800")]
    [InlineData("52428800")]
    [InlineData("UploadLimit")]
    public void MaxFileSize_EmitsTheSameLimitInAngularAndUploadValidation(string literal)
    {
        string source = UploadSource(literal);
        string angular = Generate(typeof(NgBaseDetailsGenerator), "components/base-details.generated.ts", source);
        string backend = GeneratorTestHarness.Run<ServicesGenerator>(source).GetRunResult().Results.Single()
            .GeneratedSources.Single(x => x.HintName == "ProductService.generated.cs").SourceText.ToString();

        string angularLimit = Regex.Match(angular, @"\[maxFileSize\]=""([0-9]+)""").Groups[1].Value;
        string backendLimit = Regex.Match(backend, @"Helper\.ValidateFileSize\(file\.Length, ([0-9_]+),").Groups[1].Value;
        Assert.Multiple(
            () => Assert.Equal("52428800", angularLimit),
            () => Assert.Equal("52428800", backendLimit.Replace("_", "")),
            () => Assert.Contains("[acceptedFileTypes]=\"['video/mp4', 'video/webm']\"", angular));
    }

    [Theory]
    [InlineData("52_428_800", false)]
    [InlineData("0", true)]
    [InlineData("-1", true)]
    [InlineData("UnknownLimit", true)]
    public void MaxFileSize_ReportsInvalidValuesAtTheProperty(string literal, bool invalid)
    {
        var diagnostics = GeneratorTestHarness.Run<EntityValidationGenerator>(UploadSource(literal)).GetRunResult().Diagnostics;
        if (!invalid)
        {
            Assert.Empty(diagnostics);
            return;
        }

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY032", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("Product.VideoUrl", diagnostic.GetMessage());
        Assert.Equal("VideoUrl", diagnostic.Location.SourceTree!.GetText().ToString(diagnostic.Location.SourceSpan));
    }

    private static string UploadSource(string literal) => $$"""
            using Spiderly.Shared.Attributes;
            using Spiderly.Shared.Attributes.Entity;
            using Spiderly.Shared.BaseEntities;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Product : BusinessObject<long>
                {
                    private const int UploadLimit = 52_428_800;
                    private const string Mp4 = "video/mp4";
                    private const string Webm = "video/webm";

                    [DisplayName]
                    public string Name { get; set; }

                    [S3PublicStorage]
                    [AcceptedFileTypes(Mp4, Webm)]
                    [MaxFileSize({{literal}})]
                    public string VideoUrl { get; set; }
                }
            }

            namespace TestApp.Business.Services
            {
                [SpiderlyService]
                public class ProductService : ProductServiceGenerated { }
            }
            """;

    [Theory]
    [InlineData("private static")]
    [InlineData("private")]
    [InlineData("public static")]
    public void ApiService_ControllerWithHelper_EmitsThePublicAction(string modifiers)
    {
        string source = $$"""
            using Microsoft.AspNetCore.Mvc;
            using Spiderly.Shared.Attributes;

            namespace TestApp.WebAPI.Controllers
            {
                [SpiderlyController]
                public class ReportController : ControllerBase
                {
                    [HttpGet]
                    public string GetReportName() => FormatReportName("sales");

                    {{modifiers}} string FormatReportName(string name) => $"{name}.csv";
                }
            }
            """;

        string text = Generate(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", source);

        Assert.Contains("getReportName = (", text);
        Assert.DoesNotContain("formatReportName", text, StringComparison.OrdinalIgnoreCase);
    }

    // How a [Route] template becomes the client's URL: NgControllersGenerator.GetControllerUrlPath.
    [Theory]
    [InlineData("OrderController", "", "Order")]
    [InlineData("OrderController", "[Route(\"/api/[controller]/[action]\")]", "Order")]
    [InlineData("OrderController", "[Route(\"api/[controller]/[action]\")]", "Order")]
    [InlineData("OrderFulfilmentController", "[Route(\"/api/Order/[action]\")]", "Order")]
    [InlineData("OrderFulfilmentController", "[Microsoft.AspNetCore.Mvc.Route(\"/api/Order/[action]\")]", "Order")]
    [InlineData("PaymentIntesaController", "[Route(\"/api/Payment/Intesa/[action]\")]", "Payment/Intesa")]
    public void ApiService_ControllerRoute_GivesTheUrlTheBackendServes(string controller, string route, string urlPath)
    {
        (string text, IReadOnlyList<Diagnostic> diagnostics) = GenerateWithDiagnostics(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", RouteSource(controller, route));

        Assert.Multiple(
            () => Assert.Contains($"this.http.get<number>(`${{this.config.apiUrl}}/{urlPath}/GetOrderCount${{params}}`", text),
            () => Assert.Contains($"this.http.post(`${{this.config.apiUrl}}/{urlPath}/UploadInvoice`, formData", text),
            () => Assert.Empty(diagnostics));
    }

    [Fact]
    public void ApiService_BaseControllerRoute_LosesToTheControllersOwnRoute()
    {
        string source = RouteSource("OrderFulfilmentController", "[Route(\"/api/Order/[action]\")]")
            .Replace("public class OrderFulfilmentController : ControllerBase", "public class OrderFulfilmentController : RoutedControllerBase")
            + """

            namespace TestApp.WebAPI.Controllers
            {
                [Route("/api/[controller]/[action]")]
                public class RoutedControllerBase : ControllerBase { }
            }
            """;

        string text = Generate(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", source);

        Assert.Contains("`${this.config.apiUrl}/Order/GetOrderCount${params}`", text);
    }

    [Theory]
    [InlineData("[Route(\"/api/Order\")]")]
    [InlineData("[Route(\"/api/[action]\")]")]
    [InlineData("[Route(\"/v2/Order/[action]\")]")]
    [InlineData("[Route(\"/api/Order/{id}/[action]\")]")]
    [InlineData("[Route(\"/api/Order/[action]\", Name = \"orders\")]")]
    public void ApiService_UnreadableControllerRoute_GivesTodaysUrlAndAWarning(string route)
    {
        (string text, IReadOnlyList<Diagnostic> diagnostics) = GenerateWithDiagnostics(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", RouteSource("OrderFulfilmentController", route));

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Multiple(
            () => Assert.Contains("`${this.config.apiUrl}/OrderFulfilment/GetOrderCount${params}`", text),
            () => Assert.Contains("`${this.config.apiUrl}/OrderFulfilment/UploadInvoice`", text),
            () => Assert.Equal("SPIDERLY033", diagnostic.Id),
            () => Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity),
            () => Assert.Contains("OrderFulfilmentController", diagnostic.GetMessage()));
    }

    [Fact]
    public void ApiService_UIDoNotGenerateController_GetsNoRouteWarning()
    {
        string source = RouteSource("OrderFulfilmentController", "[UIDoNotGenerate]\n[Route(\"/v2/Order/[action]\")]");

        (_, IReadOnlyList<Diagnostic> diagnostics) = GenerateWithDiagnostics(typeof(NgControllersGenerator), "services/api/api.service.generated.ts", source);

        Assert.Empty(diagnostics);
    }

    // NgControllersGenerator reads a DTO only from a referenced assembly, so the [FromForm] method takes a Spiderly.Security DTO.
    private static string RouteSource(string controller, string route) => $$"""
            using Microsoft.AspNetCore.Mvc;
            using Spiderly.Security.DTO;
            using Spiderly.Shared.Attributes;

            namespace TestApp.WebAPI.Controllers
            {
                [SpiderlyController]
                {{route}}
                public class {{controller}} : ControllerBase
                {
                    [HttpGet]
                    public int GetOrderCount(long brandId) => 0;

                    [HttpPost]
                    public void UploadInvoice([FromForm] LoginVerificationTokenDTO dto) { }
                }
            }
            """;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void BaseDetails_OneToOneNavigation_DoesNotRenderItsForeignKeyAsANumber(bool hidden)
    {
        string source = $$"""
            #nullable enable
            using Spiderly.Shared.Attributes.Entity;
            using Spiderly.Shared.Attributes.Entity.UI;
            using Spiderly.Shared.BaseEntities;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Conversation : BusinessObject<long>
                {
                    public int Sequence { get; set; }
                    public long? OwningTaskItemId { get; set; }

                    [WithOne(nameof(TaskItem.Conversation))]
                    {{(hidden ? "[UIDoNotGenerate]" : string.Empty)}}
                    public virtual TaskItem? OwningTaskItem { get; set; }
                }

                [SpiderlyEntity]
                public class TaskItem : BusinessObject<long>
                {
                    [DisplayName]
                    public string Title { get; set; } = string.Empty;
                    public virtual Conversation? Conversation { get; set; }
                }
            }
            """;

        string text = Generate(typeof(NgBaseDetailsGenerator), "components/base-details.generated.ts", source);
        const string control = " [control]=\"this.parentFormGroup.controls.conversationDTO.getControl(";
        const string foreignKey = control + "'owningTaskItemId')\"";

        Assert.Contains("<spiderly-number" + control + "'sequence')\"", text);
        if (hidden)
            Assert.DoesNotContain("<spiderly-autocomplete" + foreignKey, text);
        else
            Assert.Contains("<spiderly-autocomplete" + foreignKey, text);
        Assert.DoesNotContain("<spiderly-number" + foreignKey, text);
    }

    private string Generate(Type generatorType, string file, string source = AppSource)
        => GenerateWithDiagnostics(generatorType, file, source).Text;

    private (string Text, IReadOnlyList<Diagnostic> Diagnostics) GenerateWithDiagnostics(Type generatorType, string file, string source)
    {
        GeneratorDriverRunResult result = GeneratorTestHarness.Run(generatorType, source, projectDir: ProjectDir).GetRunResult();
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        string path = Path.Combine(BusinessFolder, file);
        Assert.True(File.Exists(path), $"{generatorType.Name} wrote no {file}.");

        return (File.ReadAllText(path).Replace("\r\n", "\n"), result.Diagnostics);
    }
}
