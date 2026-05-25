using System.Collections.Generic;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Unit tests for the shell (panel/save/auth/lifecycle) component model builder.
public class NgShellModelBuilderTests
{
    private static SpiderlyClass Brand() => new()
    {
        Name = "Brand",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
    };

    [Fact]
    public void Build_SetsShellNames()
    {
        ShellComponentModel model = NgShellModelBuilder.Build(Brand());

        Assert.Equal("Brand", model.EntityName);
        Assert.Equal("brand-base-details", model.Selector);
        Assert.Equal("BrandBaseDetailsComponent", model.ComponentClassName);
        Assert.Equal("brand-fields", model.FieldsSelector);
        Assert.Equal("BrandFieldsComponent", model.FieldsComponentClassName);
        Assert.Equal("BrandSaveBody", model.SaveBodyTypeName);
        Assert.Equal("BrandMainUIForm", model.MainUIFormTypeName);
        Assert.Equal("BrandFieldsConfig", model.ConfigClassName);
    }

    [Fact]
    public void Build_PlainEntity_DefaultsToNotAuthorized()
    {
        // A plain entity (no [DoNotAuthorize]) requires a permission to save -> default isAuthorizedForSave = false.
        Assert.False(NgShellModelBuilder.Build(Brand()).DefaultAuthorized);
    }
}
