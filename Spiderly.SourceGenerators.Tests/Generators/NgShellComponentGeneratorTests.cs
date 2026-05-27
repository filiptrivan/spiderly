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

    [Fact]
    public Task EmitsShellWithOwnComplexManyToManySeeding()
    {
        SpiderlyClass warehouse = new()
        {
            Name = "Warehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Warehouse" } },
        };
        SpiderlyClass junction = new()
        {
            Name = "ProductVariantWarehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" }, new() { Name = "M2M" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "ProductVariant", Type = "ProductVariant", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } } },
                new() { Name = "Warehouse", Type = "Warehouse", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } } },
                new() { Name = "Stock", Type = "int", EntityName = "ProductVariantWarehouse" },
            },
        };
        SpiderlyClass productVariant = new()
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

        ShellComponentModel model = NgShellModelBuilder.Build(productVariant, new() { productVariant, junction, warehouse });
        return Verify(NgShellComponentGenerator.BuildShellComponent(model));
    }

    [Fact]
    public Task EmitsShellWithOrderedChildComplexManyToManySeeding()
    {
        SpiderlyClass warehouse = new()
        {
            Name = "Warehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Warehouse" } },
        };
        SpiderlyClass junction = new()
        {
            Name = "ProductVariantWarehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" }, new() { Name = "M2M" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "ProductVariant", Type = "ProductVariant", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } } },
                new() { Name = "Warehouse", Type = "Warehouse", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } } },
                new() { Name = "Stock", Type = "int", EntityName = "ProductVariantWarehouse" },
            },
        };
        SpiderlyClass productVariant = new()
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
        SpiderlyClass product = new()
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

        ShellComponentModel model = NgShellModelBuilder.Build(product, new() { product, productVariant, junction, warehouse });
        return Verify(NgShellComponentGenerator.BuildShellComponent(model));
    }
}
