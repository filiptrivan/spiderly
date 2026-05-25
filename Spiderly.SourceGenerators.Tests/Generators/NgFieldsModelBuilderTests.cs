using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Unit tests for the structured emission model builder. Validates that the per-field facts the fragment
// and config emitters depend on (control tag, form-control name, config show-flag, width, change output)
// are computed correctly for the data-free scalar controls covered by this slice.
public class NgFieldsModelBuilderTests
{
    private static SpiderlyProperty Prop(string name, string type) =>
        new() { Name = name, Type = type, EntityName = "Brand" };

    private static SpiderlyClass Brand() => new()
    {
        Name = "Brand",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Name", "string"),
            Prop("Price", "decimal"),
            Prop("Stock", "int"),
            Prop("IsActive", "bool?"),
        },
    };

    [Fact]
    public void Build_SetsEntityLevelNames()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(Brand());

        Assert.Equal("Brand", model.EntityName);
        Assert.Equal("brand-fields", model.Selector);
        Assert.Equal("BrandFieldsComponent", model.ComponentClassName);
        Assert.Equal("BrandSaveBody", model.SaveBodyTypeName);
        Assert.Equal("BrandFieldsConfig", model.ConfigClassName);
        Assert.Equal("formGroup.controls.brandDTO", model.MainDtoAccess);
    }

    [Fact]
    public void Build_TextBoxField_HasNoSuffixedNamesAndNoOutput()
    {
        FieldModel name = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Name");

        Assert.Equal("spiderly-textbox", name.ControlTag);
        Assert.Equal("name", name.FormControlName);
        Assert.Equal("showName", name.ConfigShowFlagName);
        Assert.Equal("", name.ExtraControlAttributes);
        Assert.Null(name.ChangeOutput);
    }

    [Fact]
    public void Build_DecimalField_AddsDecimalAttributes()
    {
        FieldModel price = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Price");

        Assert.Equal("spiderly-number", price.ControlTag);
        Assert.Equal("price", price.FormControlName);
        // Brand.Price has no [Precision] attribute, so the scale is empty. Assert the exact current shape so an
        // empty [maxFractionDigits] can't masquerade as a populated one (the builder passes GetDecimalScale through verbatim).
        Assert.Equal(" [decimal]=\"true\" [maxFractionDigits]=\"\"", price.ExtraControlAttributes);
    }

    [Fact]
    public void Build_IntegerField_HasNoExtraAttributes()
    {
        FieldModel stock = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Stock");

        Assert.Equal("spiderly-number", stock.ControlTag);
        Assert.Equal("stock", stock.FormControlName);
        Assert.Equal("", stock.ExtraControlAttributes);
        Assert.Null(stock.ChangeOutput);
    }

    [Fact]
    public void Build_CheckBoxField_HasChangeOutput()
    {
        FieldModel isActive = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "IsActive");

        Assert.Equal("spiderly-checkbox", isActive.ControlTag);
        Assert.Equal("isActive", isActive.FormControlName);
        Assert.NotNull(isActive.ChangeOutput);
        Assert.Equal("onIsActiveChange", isActive.ChangeOutput.OutputName);
        Assert.Equal("CheckboxChangeEvent", isActive.ChangeOutput.EventType);
        Assert.Equal("onChange", isActive.ChangeOutput.ControlEventName);
    }
}
