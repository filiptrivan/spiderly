using System.IO;
using Spiderly.SourceGenerators.Angular;
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

    private string Generate(Type generatorType, string file)
    {
        GeneratorTestHarness.Run(generatorType, AppSource, projectDir: ProjectDir);

        string path = Path.Combine(BusinessFolder, file);
        Assert.True(File.Exists(path), $"{generatorType.Name} wrote no {file}.");

        return File.ReadAllText(path).Replace("\r\n", "\n");
    }
}
