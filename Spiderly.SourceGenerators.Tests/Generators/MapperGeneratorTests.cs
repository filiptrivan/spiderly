using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class MapperGeneratorTests
{
    private const string M2OSource = """
        using System.Collections.Generic;

        namespace TestApp.Business.Entities
        {
            [SpiderlyEntity]
            public class Category : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                public virtual List<Product> Products { get; } = new();
            }

            [SpiderlyEntity]
            public class Product : BusinessObject<long>
            {
                [DisplayName]
                public string Name { get; set; }

                public decimal Price { get; set; }

                [WithMany(nameof(Category.Products))]
                public virtual Category Category { get; set; }
            }
        }

        namespace TestApp.Business.DataMappers
        {
            [SpiderlyDataMapper]
            public partial class Mapper { }
        }
        """;

    // Mapster's convention flattening silently projects a dest prop like "ShippingTierIsBulky"
    // through an OPTIONAL nav (src.ShippingTier.IsBulky) — a LEFT JOIN NULL then crashes the EF
    // shaper on any non-nullable member ("Nullable object must have a value"; PACMS BACKEND-RS-1C
    // took down the admin order grid). Generated configs must strip the strategy, and the
    // strongly-typed escape hatch for deliberate custom mappings is a Customize* partial hook
    // per config method — real C#, compiler-checked, null-guards expressible.
    [Fact]
    public void GeneratedConfigs_DisableFlattening_AndExposeCustomizePartialHooks()
    {
        var driver = GeneratorTestHarness.Run<MapperGenerator>(M2OSource);
        string mapper = driver.GetRunResult().Results.Single().GeneratedSources
            .Single(s => s.HintName == "Mapper.generated.cs").SourceText.ToString();

        Assert.Contains("ValueAccessingStrategies.Remove(ValueAccessingStrategy.FlattenMember)", mapper);

        foreach (string hook in new[]
        {
            "CustomizeProductDTOToEntityConfig",
            "CustomizeProductToDTOConfig",
            "CustomizeProductProjectToConfig",
            "CustomizeProductExcelProjectToConfig",
        })
        {
            Assert.Contains($"static partial void {hook}(TypeAdapterConfig config);", mapper);
            Assert.Contains($"{hook}(config);", mapper);
        }
    }

    [Fact]
    public Task EntityWithM2OAndDataMapper_EmitsMapperConfigs()
    {
        var driver = GeneratorTestHarness.Run<MapperGenerator>(M2OSource);
        return Verify(driver);
    }
}
