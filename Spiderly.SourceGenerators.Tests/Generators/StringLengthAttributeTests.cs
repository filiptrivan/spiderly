using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class StringLengthAttributeTests
{
    [Fact]
    public void StringLength_WithNamedConstants_GeneratesBothLengthBounds()
    {
        const string productSource = """
            using System.ComponentModel.DataAnnotations;
            using Spiderly.Shared.Attributes.Entity;
            using Spiderly.Shared.BaseEntities;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Product : BusinessObject<long>
                {
                    [Required]
                    [StringLength(NameLimits.Max, MinimumLength = NameLimits.Min)]
                    public string Name { get; set; } = string.Empty;
                }
            }
            """;

        SyntaxTree product = CSharpSyntaxTree.ParseText(productSource, path: "Product.cs");
        SyntaxTree limits = ParseLimits(3, 20);
        var compilation = CSharpCompilation.Create("TestApp.Business", [product, limits], TestMetadataReferences.All,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new FluentValidationGenerator().AsSourceGenerator());

        driver = driver.RunGenerators(compilation);
        AssertLengthRule(driver, 3, 20);

        driver = driver.RunGenerators(compilation.ReplaceSyntaxTree(limits, ParseLimits(5, 30)));
        AssertLengthRule(driver, 5, 30);
    }

    private static SyntaxTree ParseLimits(int min, int max) => CSharpSyntaxTree.ParseText($$"""
        namespace TestApp.Business.Entities;

        public static class NameLimits
        {
            public const int Min = {{min}};
            public const int Max = {{max}};
        }
        """, path: "NameLimits.cs");

    private static void AssertLengthRule(GeneratorDriver driver, int min, int max)
    {
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);

        string rules = Assert.Single(result.GeneratedSources).SourceText.ToString();
        Assert.Contains($"RuleFor(x => x.Name).NotEmpty().Length({min}, {max});", rules);
    }
}
