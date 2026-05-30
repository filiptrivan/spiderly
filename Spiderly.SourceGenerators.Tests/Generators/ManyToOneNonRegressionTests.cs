using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class ManyToOneNonRegressionTests
{
    // Pure M2O — NO [WithOne] anywhere. Pins M2O mapper output so the 1-1 carve-out
    // and later DTO/cascade edits to the shared M2O branches can't silently regress it.
    [Fact]
    public Task ManyToOne_MapperOutput_Unchanged()
    {
        const string source = """
            using System.Collections.Generic;
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Category : BusinessObject<long>
                {
                    [DisplayName] public string Name { get; set; }
                    public virtual List<Product> Products { get; } = new();
                }
                [SpiderlyEntity]
                public class Product : BusinessObject<long>
                {
                    [DisplayName] public string Title { get; set; }
                    [Required]
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
        var driver = GeneratorTestHarness.Run<MapperGenerator>(source);
        return Verify(driver);
    }
}
