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
        Assert.Contains("[decimal]=\"true\"", price.ExtraControlAttributes);
        Assert.Contains("[maxFractionDigits]=", price.ExtraControlAttributes);
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
