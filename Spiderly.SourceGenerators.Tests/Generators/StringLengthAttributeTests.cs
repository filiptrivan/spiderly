using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class StringLengthAttributeTests
{
    [Fact]
    public void StringLength_WithNamedConstants_GeneratesBothLengthBounds()
    {
        const string source = """
            using System.ComponentModel.DataAnnotations;
            using Spiderly.Shared.Attributes.Entity;
            using Spiderly.Shared.BaseEntities;

            namespace TestApp.Business.Entities
            {
                public static class NameLimits
                {
                    public const int Min = 3;
                    public const int Max = 20;
                }

                [SpiderlyEntity]
                public class Product : BusinessObject<long>
                {
                    [Required]
                    [StringLength(NameLimits.Max, MinimumLength = NameLimits.Min)]
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        GeneratorRunResult result = GeneratorTestHarness.Run<FluentValidationGenerator>(source)
            .GetRunResult().Results.Single();

        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        string rules = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains("RuleFor(x => x.Name).NotEmpty().Length(3, 20);", rules);
    }
}
