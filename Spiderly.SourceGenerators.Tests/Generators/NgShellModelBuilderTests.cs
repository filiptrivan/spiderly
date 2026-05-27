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
        ShellComponentModel model = NgShellModelBuilder.Build(Brand(), new() { Brand() });

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
        Assert.False(NgShellModelBuilder.Build(Brand(), new() { Brand() }).DefaultAuthorized);
    }

    private static SpiderlyClass BrandWithExtraAuth() => new()
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

    [Fact]
    public void Build_PopulatesAdditionalSavePermissionCodes()
    {
        ShellComponentModel model = NgShellModelBuilder.Build(BrandWithExtraAuth(), new() { BrandWithExtraAuth() });

        Assert.Equal(2, model.AdditionalSavePermissionCodes.Count);
        Assert.Equal("ExtraInsert", model.AdditionalSavePermissionCodes[0].PermissionCode);
        Assert.True(model.AdditionalSavePermissionCodes[0].ForInsert);
        Assert.Equal("ExtraUpdate", model.AdditionalSavePermissionCodes[1].PermissionCode);
        Assert.False(model.AdditionalSavePermissionCodes[1].ForInsert);
    }

    [Fact]
    public void Build_NoExtraAuth_LeavesEmpty()
    {
        Assert.Empty(NgShellModelBuilder.Build(Brand(), new() { Brand() }).AdditionalSavePermissionCodes);
    }

    private static SpiderlyClass SeedProductVariant() => new()
    {
        Name = "ProductVariant", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            new() { Name = "Name", Type = "string", EntityName = "ProductVariant" },
            new() { Name = "ProductVariantWarehouses", Type = "List<ProductVariantWarehouse>", EntityName = "ProductVariant",
                Attributes = new List<SpiderlyAttribute> { new() { Name = "ComplexManyToManyList" } } },
        },
    };

    private static SpiderlyClass SeedProduct() => new()
    {
        Name = "Product", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            new() { Name = "Name", Type = "string", EntityName = "Product" },
            new() { Name = "ProductVariants", Type = "List<ProductVariant>", EntityName = "Product",
                Attributes = new List<SpiderlyAttribute> { new() { Name = "UIOrderedOneToMany" } } },
        },
    };

    [Fact]
    public void Build_OwnComplexManyToManyList_SeedsForkJoinAndNewEntityInit()
    {
        ShellComponentModel model = NgShellModelBuilder.Build(
            SeedProductVariant(), new() { SeedProductVariant() });

        Assert.Equal(
            new[] { "defaultProductVariantWarehousesForProductVariant: this.apiService.getDefaultProductVariantWarehousesForProductVariant()" },
            model.SeedForkJoinParams.ToArray());
        Assert.Equal(
            new[] { "productVariantWarehouses: data.defaultProductVariantWarehousesForProductVariant" },
            model.NewEntitySeedInits.ToArray());
        Assert.Empty(model.OrderedChildSeedAssignments);
    }

    [Fact]
    public void Build_OrderedChildComplexManyToManyList_SeedsForkJoinAndFormArrayAssignment()
    {
        ShellComponentModel model = NgShellModelBuilder.Build(
            SeedProduct(), new() { SeedProduct(), SeedProductVariant() });

        Assert.Equal(
            new[] { "defaultProductVariantWarehousesForProductVariant: this.apiService.getDefaultProductVariantWarehousesForProductVariant()" },
            model.SeedForkJoinParams.ToArray());
        Assert.Empty(model.NewEntitySeedInits);
        Assert.Equal(
            new[] { "this.parentFormGroup.controls.orderedProductVariantsSaveBodyDTO.formGroupInitialValues = { ...this.parentFormGroup.controls.orderedProductVariantsSaveBodyDTO.formGroupInitialValues, productVariantWarehouses: data.defaultProductVariantWarehousesForProductVariant };" },
            model.OrderedChildSeedAssignments.ToArray());
    }

    [Fact]
    public void Build_NoComplexManyToManyList_LeavesSeedListsEmpty()
    {
        ShellComponentModel model = NgShellModelBuilder.Build(Brand(), new() { Brand() });

        Assert.Empty(model.SeedForkJoinParams);
        Assert.Empty(model.NewEntitySeedInits);
        Assert.Empty(model.OrderedChildSeedAssignments);
    }
}
