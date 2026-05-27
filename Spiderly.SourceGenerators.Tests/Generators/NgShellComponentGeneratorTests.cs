using System.Collections.Generic;
using System.Threading.Tasks;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Snapshot of the {Entity}BaseDetails shell: panel + Save/auth/return footer + route/load lifecycle, embedding
// the {Entity}Fields fragment. Single-panel minimal scope (no UISection/M2M/ordered/dropdown-loading).
public class NgShellComponentGeneratorTests
{
    [Fact]
    public Task EmitsShellComponent()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        };

        ShellComponentModel model = NgShellModelBuilder.Build(brand, new() { brand });

        return Verify(NgShellComponentGenerator.BuildShellComponent(model));
    }

    [Fact]
    public Task EmitsShellComponentWithAdditionalSaveAuth()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "SpiderlyEntity" },
                new() { Name = "UIAdditionalPermissionCodeForInsert", Value = "ExtraInsert" },
                new() { Name = "UIAdditionalPermissionCodeForUpdate", Value = "ExtraUpdate" },
            },
        };

        ShellComponentModel model = NgShellModelBuilder.Build(brand, new() { brand });
        return Verify(NgShellComponentGenerator.BuildShellComponent(model));
    }
}
