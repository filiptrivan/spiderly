using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class ServicesGeneratorTests
{
    [Fact]
    public Task EntityWithOneToManyAndCustomService_EmitsServiceClass()
    {
        const string source = """
            using System.Collections.Generic;

            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Tag : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    public string Name { get; set; }

                    [WithMany(nameof(Product.Tags))]
                    public virtual List<Product> Products { get; } = new();
                }

                [SpiderlyEntity]
                public class Product : BusinessObject<long>
                {
                    [DisplayName]
                    public string Name { get; set; }

                    public virtual List<Tag> Tags { get; } = new();
                }
            }

            namespace TestApp.Business.Services
            {
                [SpiderlyService]
                public class ProductService : ProductServiceGenerated { }
            }
            """;

        var driver = GeneratorTestHarness.Run<ServicesGenerator>(source);
        return Verify(driver);
    }
}
