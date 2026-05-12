using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class MapperGeneratorTests
{
    [Fact]
    public Task EntityWithM2OAndDataMapper_EmitsMapperConfigs()
    {
        const string source = """
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

        var driver = GeneratorTestHarness.Run<MapperGenerator>(source);
        return Verify(driver);
    }
}
