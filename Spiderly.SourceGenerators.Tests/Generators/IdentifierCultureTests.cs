using System.Globalization;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class IdentifierCultureTests
{
    [Theory]
    [InlineData("en-US", "IsFragile", "isFragile")]
    [InlineData("en-US", "Id", "id")]
    [InlineData("tr-TR", "IsFragile", "isFragile")]
    [InlineData("tr-TR", "Id", "id")]
    public void EntityProperty_UsesCultureIndependentIdentifier(string culture, string property, string expected)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        string root = Path.Combine(Path.GetTempPath(), "spiderly-culture-" + Guid.NewGuid().ToString("N"));
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo(culture);
            Directory.CreateDirectory(Path.Combine(root, "Frontend", "src", "app", "business", "entities"));
            string source = $$"""
                using Spiderly.Shared.Attributes.Entity;
                namespace TestApp.Business.DTO
                {
                    [SpiderlyDTO]
                    public class ShipmentDTO
                    {
                        public bool? IsFragile { get; set; }
                        public long Id { get; set; }
                    }
                }
                """;
            var result = GeneratorTestHarness.Run<NgEntitiesGenerator>(source,
                projectDir: Path.Combine(root, "Backend", "TestApp.Business")).GetRunResult();
            Assert.DoesNotContain(result.Diagnostics, x => x.Severity == DiagnosticSeverity.Error);
            string output = File.ReadAllText(Path.Combine(root, "Frontend", "src", "app", "business", "entities", "entities.generated.ts"));
            Assert.True(output.Contains(expected + "?:", StringComparison.Ordinal),
                $"{property} under {culture} should generate {expected}.{Environment.NewLine}{output}");
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
