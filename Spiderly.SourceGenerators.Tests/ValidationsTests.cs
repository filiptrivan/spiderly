using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class ValidationsTests
{
    #region ValidateWithManyAttributes

    [Fact]
    public void ValidateWithManyAttributes_WellFormedManyToOne_YieldsNoDiagnostics()
    {
        SpiderlyClass order = Entity("Order",
            Prop("OrderItems", "List<OrderItem>"));

        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "Order", withMany: "OrderItems"));

        List<SpiderlyClass> entities = new() { order, orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateWithManyAttributes_MissingWithMany_YieldsSPIDERLY015()
    {
        SpiderlyClass order = Entity("Order",
            Prop("OrderItems", "List<OrderItem>"));

        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "Order"));

        List<SpiderlyClass> entities = new() { order, orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY015", diagnostic.Id);
        Assert.Contains("OrderItem", diagnostic.GetMessage());
        Assert.Contains("Order", diagnostic.GetMessage());
        Assert.Contains("[WithMany", diagnostic.GetMessage());
    }

    [Fact]
    public void ValidateWithManyAttributes_BackCollectionDoesNotExist_YieldsSPIDERLY016()
    {
        SpiderlyClass order = Entity("Order");

        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "Order", withMany: "OrderItems"));

        List<SpiderlyClass> entities = new() { order, orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY016", diagnostic.Id);
        Assert.Contains("OrderItems", diagnostic.GetMessage());
        Assert.Contains("Order", diagnostic.GetMessage());
    }

    [Fact]
    public void ValidateWithManyAttributes_BackCollectionWrongElementType_YieldsSPIDERLY017()
    {
        SpiderlyClass order = Entity("Order",
            Prop("OrderItems", "List<Customer>"));

        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "Order", withMany: "OrderItems"));

        List<SpiderlyClass> entities = new() { order, orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY017", diagnostic.Id);
        Assert.Contains("Customer", diagnostic.GetMessage());
        Assert.Contains("OrderItem", diagnostic.GetMessage());
    }

    [Fact]
    public void ValidateWithManyAttributes_BackCollectionIsScalarType_YieldsSPIDERLY017()
    {
        SpiderlyClass order = Entity("Order",
            Prop("OrderItems", "string"));

        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "Order", withMany: "OrderItems"));

        List<SpiderlyClass> entities = new() { order, orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY017", diagnostic.Id);
    }

    [Fact]
    public void ValidateWithManyAttributes_M2MWithManyNavigation_IsSkipped()
    {
        SpiderlyClass cart = Entity("Cart",
            Prop("CartItems", "List<CartItem>"));

        SpiderlyClass cartItem = Entity("CartItem",
            Nav("Cart", "Cart", m2mWithMany: "CartItems"));

        List<SpiderlyClass> entities = new() { cart, cartItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateWithManyAttributes_TargetTypeNotASpiderlyEntity_IsSkipped()
    {
        SpiderlyClass orderItem = Entity("OrderItem",
            Nav("Order", "SomeExternalType", withMany: "OrderItems"));

        List<SpiderlyClass> entities = new() { orderItem };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ValidateWithManyAttributes_AcceptsIListBackCollection()
    {
        SpiderlyClass user = Entity("User",
            Prop("Refunds", "IList<Refund>"));

        SpiderlyClass refund = Entity("Refund",
            Nav("User", "User", withMany: "Refunds"));

        List<SpiderlyClass> entities = new() { user, refund };

        Diagnostic[] diagnostics = Validations.ValidateWithManyAttributes(entities, entities).ToArray();

        Assert.Empty(diagnostics);
    }

    #endregion

    private static SpiderlyClass Entity(string name, params SpiderlyProperty[] properties)
    {
        return new SpiderlyClass
        {
            Name = name,
            Namespace = "Test.Entities",
            Properties = properties.ToList(),
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        };
    }

    private static SpiderlyProperty Prop(string name, string type)
    {
        return new SpiderlyProperty { Name = name, Type = type };
    }

    private static SpiderlyProperty Nav(string name, string type, string withMany = null, string m2mWithMany = null)
    {
        SpiderlyProperty property = new() { Name = name, Type = type };
        if (withMany != null)
            property.Attributes.Add(new SpiderlyAttribute { Name = "WithMany", Value = withMany });
        if (m2mWithMany != null)
            property.Attributes.Add(new SpiderlyAttribute { Name = "M2MWithMany", Value = m2mWithMany });
        return property;
    }
}
