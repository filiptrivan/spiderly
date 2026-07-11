using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// Pins the rendered field set of a ComplexManyToManyList junction row. The card already names its
/// other-side entity in the header (the {OtherSide}DisplayName control) and the current side is the
/// form's own parent, so the two FK scalars are row identity, not editable data. Regression: the
/// generated product form rendered productVariantId / warehouseId as raw editable number inputs in
/// every warehouse-stock row.
/// </summary>
public class ComplexManyToManyListUiTests
{
    [Fact]
    public void JunctionRow_RendersDataColumnsOnly_NoFkScalarInputs()
    {
        SpiderlyProperty itemWarehousesOnItem = new()
        {
            Name = "ItemWarehouses",
            Type = "List<ItemWarehouse>",
            Attributes = { new SpiderlyAttribute { Name = "ComplexManyToManyList" } },
        };

        SpiderlyClass item = new()
        {
            Name = "Item",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Properties = { new SpiderlyProperty { Name = "Name", Type = "string" }, itemWarehousesOnItem },
        };

        SpiderlyClass warehouse = new()
        {
            Name = "Warehouse",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<byte>",
            Properties =
            {
                new SpiderlyProperty { Name = "Name", Type = "string" },
                new SpiderlyProperty { Name = "ItemWarehouses", Type = "List<ItemWarehouse>" },
            },
        };

        SpiderlyClass itemWarehouse = new()
        {
            Name = "ItemWarehouse",
            Namespace = "TestApp.Business.Entities",
            Attributes = { new SpiderlyAttribute { Name = "M2M" }, new SpiderlyAttribute { Name = "SpiderlyEntity" } },
            Properties =
            {
                new SpiderlyProperty { Name = "ItemId", Type = "long" },
                new SpiderlyProperty
                {
                    Name = "Item",
                    Type = "Item",
                    Attributes = { new SpiderlyAttribute { Name = "M2MWithMany", Value = "ItemWarehouses" } },
                },
                new SpiderlyProperty { Name = "WarehouseId", Type = "byte" },
                new SpiderlyProperty
                {
                    Name = "Warehouse",
                    Type = "Warehouse",
                    Attributes = { new SpiderlyAttribute { Name = "M2MWithMany", Value = "ItemWarehouses" } },
                },
                new SpiderlyProperty { Name = "Stock", Type = "int" },
            },
        };

        List<SpiderlyClass> allEntities = new() { item, warehouse, itemWarehouse };

        string html = NgDetailsPropertyBlockGenerator.GetComplexManyToManyListBlock(
            item, itemWarehousesOnItem, allEntities, isFromOrderedOneToMany: false);

        Assert.Contains("getControl('stock')", html);
        Assert.Contains("getControl('warehouseDisplayName')", html); // the card header names the row
        Assert.DoesNotContain("getControl('itemId')", html);
        Assert.DoesNotContain("getControl('warehouseId')", html);
    }
}
