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

    // Regression guard for [ExcludeFromDTO] on collection control types. These properties are
    // synthesized into the SaveBody/MainUIForm DTOs by their own loops (GetSaveBodyDTOProperties /
    // GetMainUIFormDTOProperties), which historically ignored [ExcludeFromDTO] and leaked
    // Selected*Ids / *NamebookDTOList / Ordered*SaveBodyDTO fields. The kept twin of each control
    // type must still appear; the excluded twin must be absent everywhere in the snapshot.
    [Fact]
    public Task ExcludeFromDTO_OnCollectionControls_DropsThemFromSaveBodyAndMainUIForm()
    {
        const string source = """
            namespace TestApp.Business.Entities
            {
                [SpiderlyEntity]
                public class Tag : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    [StringLength(100, MinimumLength = 1)]
                    public string Name { get; set; }
                }

                [SpiderlyEntity]
                public class Article : BusinessObject<long>
                {
                    [DisplayName]
                    [Required]
                    [StringLength(200, MinimumLength = 1)]
                    public string Title { get; set; }

                    [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
                    public virtual List<Tag> KeptTags { get; } = new();

                    [ExcludeFromDTO]
                    [UIControlType(nameof(UIControlTypeCodes.MultiSelect))]
                    public virtual List<Tag> SecretTags { get; } = new();

                    [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
                    public virtual List<Tag> KeptContributors { get; } = new();

                    [ExcludeFromDTO]
                    [UIControlType(nameof(UIControlTypeCodes.MultiAutocomplete))]
                    public virtual List<Tag> SecretContributors { get; } = new();
                }
            }
            """;

        var driver = GeneratorTestHarness.Run<EntitiesToDTOGenerator>(source);
        return Verify(driver);
    }
}
