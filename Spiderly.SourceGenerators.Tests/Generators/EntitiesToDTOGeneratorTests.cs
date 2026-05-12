using Spiderly.SourceGenerators.Net;
using Spiderly.SourceGenerators.Tests.Infrastructure;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class EntitiesToDTOGeneratorTests
{
    [Fact]
    public Task EntityWithDisplayNameAndHiddenProperty_EmitsDTOs()
    {
        const string source = """
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Brand : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    [StringLength(255, MinimumLength = 1)]
                    public string Name { get; set; }

                    [StringLength(400)]
                    public string Slug { get; set; }

                    [UIDoNotGenerate]
                    public string InternalNote { get; set; }

                    public bool? IsActive { get; set; }
                }
            }

            namespace TestApp.Business.DTO
            {
                [SpiderlyDTO]
                public partial class BrandCustomFacetDTO
                {
                    public long Id { get; set; }
                    public int ProductCount { get; set; }
                }
            }
            """;

        var driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(source);
        return Verify(driver);
    }
}
