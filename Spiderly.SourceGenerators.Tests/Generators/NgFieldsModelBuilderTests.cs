using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Unit tests for the structured emission model builder. Validates per-field facts for scalar controls
// (TextBox, Integer, Decimal, CheckBox) and M2O Autocomplete controls.
public class NgFieldsModelBuilderTests
{
    private static SpiderlyProperty Prop(string name, string type, params (string Name, string? Value)[] attributes) =>
        new()
        {
            Name = name,
            Type = type,
            EntityName = "Brand",
            Attributes = attributes.Select(a => new SpiderlyAttribute { Name = a.Name, Value = a.Value }).ToList(),
        };

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
            Prop("Country", "Country"),
            new() { Name = "Status", Type = "BrandStatusCodes", EntityName = "Brand", IsEnum = true },
            Prop("Description", "string", ("UIControlType", "TextArea")),
            Prop("Secret", "string", ("UIControlType", "Password")),
            Prop("Info", "string", ("UIControlType", "TextBlock")),
            Prop("Tags", "List<Tag>", ("UIControlType", "MultiSelect")),
            Prop("Authors", "List<Author>", ("UIControlType", "MultiAutocomplete")),
            Prop("PublishedAt", "DateTime"),
            Prop("BirthDate", "DateOnly"),
            Prop("OpenTime", "TimeOnly"),
            Prop("Color", "string", ("UIControlType", "ColorPicker")),
            Prop("Content", "string", ("UIControlType", "Editor")),
            Prop("Bio", "string", ("UIControlType", "Editor"), ("S3PublicStorage", null)),
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

    [Fact]
    public void Build_M2OAutocomplete_HasOptionsFieldAndSearch()
    {
        FieldModel country = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Country");

        Assert.Equal("spiderly-autocomplete", country.ControlTag);
        Assert.Equal("countryId", country.FormControlName);
        Assert.Equal("countryOptions", country.OptionsFieldName);
        Assert.False(country.OptionsIsInput);
        Assert.NotNull(country.Search);
        Assert.Equal("searchCountry", country.Search.MethodName);
        Assert.Equal("getCountryAutocompleteListForBrand", country.Search.ApiMethodName);
        Assert.Equal("countryOptions", country.Search.OptionsFieldName);
        Assert.Contains("[options]=\"countryOptions\"", country.ExtraControlAttributes);
        Assert.Contains("[displayName]=\"formGroup.controls.brandDTO.controls.countryDisplayName.getRawValue()\"", country.ExtraControlAttributes);
        Assert.Contains("(onTextInput)=\"searchCountry($event, formGroup.controls.brandDTO.controls.id.getRawValue())\"", country.ExtraControlAttributes);
    }

    [Fact]
    public void Build_Dropdown_HasInputOptionsAndChangeOutput()
    {
        FieldModel status = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Status");

        Assert.Equal("spiderly-dropdown", status.ControlTag);
        Assert.Equal("status", status.FormControlName);
        Assert.Equal("statusOptions", status.OptionsFieldName);
        Assert.True(status.OptionsIsInput);
        Assert.Contains("[options]=\"statusOptions\"", status.ExtraControlAttributes);
        Assert.NotNull(status.ChangeOutput);
        Assert.Equal("onStatusChange", status.ChangeOutput.OutputName);
        Assert.Equal("DropdownChangeEvent", status.ChangeOutput.EventType);
        Assert.Equal("onChange", status.ChangeOutput.ControlEventName);
    }

    [Theory]
    [InlineData("Description", "spiderly-textarea")]
    [InlineData("Secret", "spiderly-password")]
    [InlineData("Info", "spiderly-textblock")]
    public void Build_SimpleScalarControls_MapToTag(string propertyName, string expectedTag)
    {
        FieldModel field = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == propertyName);

        Assert.Equal(expectedTag, field.ControlTag);
        Assert.Equal("", field.ExtraControlAttributes);
        Assert.Null(field.ChangeOutput);
        Assert.Null(field.OptionsFieldName);
    }

    [Fact]
    public void Build_MultiSelect_BindsOnSaveBodyWithInputOptionsAndLabel()
    {
        FieldModel tags = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Tags");

        Assert.Equal("spiderly-multiselect", tags.ControlTag);
        Assert.Equal("selectedTagsIds", tags.FormControlName);
        Assert.True(tags.BindsOnSaveBody);
        Assert.Equal("tagsOptions", tags.OptionsFieldName);
        Assert.True(tags.OptionsIsInput);
        Assert.Null(tags.Search);
        Assert.Null(tags.ChangeOutput);
        Assert.Contains("[options]=\"tagsOptions\"", tags.ExtraControlAttributes);
        Assert.Contains("[label]=\"t('Tags')\"", tags.ExtraControlAttributes);
    }

    [Fact]
    public void Build_MultiAutocomplete_BindsOnSaveBodyWithSelfOwnedSearchAndLabel()
    {
        FieldModel authors = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Authors");

        Assert.Equal("spiderly-multiautocomplete", authors.ControlTag);
        Assert.Equal("selectedAuthorsNamebookDTOList", authors.FormControlName);
        Assert.True(authors.BindsOnSaveBody);
        Assert.Equal("authorsOptions", authors.OptionsFieldName);
        Assert.False(authors.OptionsIsInput);
        Assert.NotNull(authors.Search);
        Assert.Equal("searchAuthors", authors.Search.MethodName);
        Assert.Equal("getAuthorsAutocompleteListForBrand", authors.Search.ApiMethodName);
        Assert.Contains("[options]=\"authorsOptions\"", authors.ExtraControlAttributes);
        Assert.Contains("(onTextInput)=\"searchAuthors($event, formGroup.controls.brandDTO.controls.id.getRawValue())\"", authors.ExtraControlAttributes);
        Assert.Contains("[label]=\"t('Authors')\"", authors.ExtraControlAttributes);
    }

    [Fact]
    public void Build_CalendarDateTime_AddsShowTimeConfigFlagDefaultFalse()
    {
        FieldModel publishedAt = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "PublishedAt");

        Assert.Equal("spiderly-calendar", publishedAt.ControlTag);
        Assert.Equal("publishedAt", publishedAt.FormControlName);
        Assert.Contains("showPublishedAtTime", publishedAt.ExtraConfigFlags);
        Assert.Equal(" [showTime]=\"config.showPublishedAtTime === true\"", publishedAt.ExtraControlAttributes);
    }

    [Fact]
    public void Build_CalendarDateOnly_UsesStaticLiteralNoExtraFlag()
    {
        FieldModel birthDate = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "BirthDate");

        Assert.Equal("spiderly-calendar", birthDate.ControlTag);
        Assert.Equal(" [dateOnly]=\"true\"", birthDate.ExtraControlAttributes);
        Assert.Empty(birthDate.ExtraConfigFlags);
    }

    [Fact]
    public void Build_CalendarTimeOnly_UsesStaticLiteralNoExtraFlag()
    {
        FieldModel openTime = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "OpenTime");

        Assert.Equal("spiderly-calendar", openTime.ControlTag);
        Assert.Equal(" [timeOnly]=\"true\"", openTime.ExtraControlAttributes);
        Assert.Empty(openTime.ExtraConfigFlags);
    }

    [Fact]
    public void Build_ColorPicker_AddsShowTextFieldConfigFlagDefaultTrue()
    {
        FieldModel color = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Color");

        Assert.Equal("spiderly-colorpicker", color.ControlTag);
        Assert.Equal("color", color.FormControlName);
        Assert.Contains("showColorTextField", color.ExtraConfigFlags);
        Assert.Equal(" [showInputTextField]=\"config.showColorTextField !== false\"", color.ExtraControlAttributes);
    }

    [Fact]
    public void Build_Editor_PlainHasNoUploadOrExtraAttributes()
    {
        FieldModel content = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Content");

        Assert.Equal("spiderly-editor", content.ControlTag);
        Assert.Equal("content", content.FormControlName);
        Assert.Equal("", content.ExtraControlAttributes);
        Assert.Null(content.EditorImageUpload);
    }

    [Fact]
    public void Build_EditorS3_HasUploadImageMethodAndObjectId()
    {
        FieldModel bio = NgFieldsModelBuilder.Build(Brand()).Fields.Single(f => f.PropertyName == "Bio");

        Assert.Equal("spiderly-editor", bio.ControlTag);
        Assert.NotNull(bio.EditorImageUpload);
        Assert.Equal("uploadBioImage", bio.EditorImageUpload.MethodName);
        Assert.Equal("uploadBioImageForBrand", bio.EditorImageUpload.ApiMethodName);
        Assert.Contains("[uploadImageMethod]=\"uploadBioImage\"", bio.ExtraControlAttributes);
        Assert.Contains("[objectId]=\"formGroup.controls.brandDTO.controls.id.getRawValue()\"", bio.ExtraControlAttributes);
    }
}
