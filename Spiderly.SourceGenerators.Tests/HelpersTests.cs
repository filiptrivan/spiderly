using System.Collections.Immutable;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class HelpersTests
{
    #region GetFormatedAttributeValue

    [Theory]
    [InlineData("\"hello\"", "hello")]
    [InlineData("@\"hello\"", "hello")]
    [InlineData("nameof(User.Email)", "Email")]
    [InlineData("nameof(Email)", "Email")]
    [InlineData("nameof(User.Roles)", "Roles")]
    [InlineData(null, null)]
    public void GetFormatedAttributeValue_VariousInputs_ReturnsCleanedValue(string? input, string? expected)
    {
        Assert.Equal(expected, ClassAnalyzer.GetFormatedAttributeValue(input));
    }

    #endregion

    #region GetBasePartOfNamespace

    [Theory]
    [InlineData("Spiderly.Security.Entities", "Spiderly.Security")]
    [InlineData("MyApp.Business.Entities", "MyApp.Business")]
    public void GetBasePartOfNamespace_VariousInputs_ReturnsBaseNamespace(string input, string expected)
    {
        Assert.Equal(expected, Helpers.GetBasePartOfNamespace(input));
    }

    #endregion

    #region GetProjectName

    [Theory]
    [InlineData("Spiderly.Security.Entities", "Security")]
    [InlineData("MyApp.Business.Entities", "Business")]
    [InlineData("Root.Sub.Deep.Entities", "Deep")]
    public void GetProjectName_VariousInputs_ReturnsProjectName(string input, string expected)
    {
        Assert.Equal(expected, Helpers.GetProjectName(input));
    }

    #endregion

    #region GetAngularType

    private static readonly ImmutableArray<string> EmptyEnumRegistry = ImmutableArray<string>.Empty;
    private static readonly ImmutableArray<string> StatusCodesEnumRegistry = ImmutableArray.Create("StatusCodes");

    [Theory]
    [InlineData("string", "string")]
    [InlineData("bool", "boolean")]
    [InlineData("bool?", "boolean")]
    [InlineData("DateTime", "Date")]
    [InlineData("DateTime?", "Date")]
    [InlineData("long", "number")]
    [InlineData("long?", "number")]
    [InlineData("int", "number")]
    [InlineData("int?", "number")]
    [InlineData("decimal", "number")]
    [InlineData("decimal?", "number")]
    [InlineData("float", "number")]
    [InlineData("float?", "number")]
    [InlineData("double", "number")]
    [InlineData("double?", "number")]
    [InlineData("byte", "number")]
    [InlineData("byte?", "number")]
    public void GetAngularType_BaseTypes_ReturnsMappedType(string cSharpType, string expected)
    {
        Assert.Equal(expected, AngularTypeMapper.GetAngularType(cSharpType, EmptyEnumRegistry));
    }

    [Fact]
    public void GetAngularType_EnumType_ReturnsSameType()
    {
        Assert.Equal("StatusCodes", AngularTypeMapper.GetAngularType("StatusCodes", StatusCodesEnumRegistry));
    }

    [Fact]
    public void GetAngularType_List_ReturnsArrayType()
    {
        string result = AngularTypeMapper.GetAngularType("List<long>", EmptyEnumRegistry);
        Assert.Equal("number[]", result);
    }

    [Fact]
    public void GetAngularType_ActionResult_ReturnsAny()
    {
        Assert.Equal("any", AngularTypeMapper.GetAngularType("ActionResult", EmptyEnumRegistry));
    }

    #endregion

    #region FindMinValueForStringLength / FindMaxValueForStringLength

    [Fact]
    public void FindMinValueForStringLength_WithMinimumLength_ReturnsMinValue()
    {
        Assert.Equal("5", ValidationRuleBuilder.FindMinValueForStringLength("70, MinimumLength = 5"));
    }

    [Fact]
    public void FindMinValueForStringLength_WithoutMinimumLength_ReturnsNull()
    {
        Assert.Null(ValidationRuleBuilder.FindMinValueForStringLength("70"));
    }

    [Fact]
    public void FindMaxValueForStringLength_StandardInput_ReturnsMaxValue()
    {
        Assert.Equal("70", ValidationRuleBuilder.FindMaxValueForStringLength("70, MinimumLength = 5"));
    }

    [Fact]
    public void FindMaxValueForStringLength_OnlyMax_ReturnsMaxValue()
    {
        Assert.Equal("100", ValidationRuleBuilder.FindMaxValueForStringLength("100"));
    }

    #endregion

    #region GetRulePartsForProperty

    [Fact]
    public void GetRulePartsForProperty_StringLength_ReturnsMaximumLengthRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Name",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "StringLength", Value = "70" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Name");

        MaximumLengthRulePart maxLengthPart = Assert.Single(result.OfType<MaximumLengthRulePart>());
        Assert.Equal(70, maxLengthPart.MaxLength);
    }

    [Fact]
    public void GetRulePartsForProperty_StringLengthWithMin_ReturnsLengthRangePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Name",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "StringLength", Value = "70, MinimumLength = 5" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Name");

        LengthRangePart lengthRangePart = Assert.Single(result.OfType<LengthRangePart>());
        Assert.Equal(5, lengthRangePart.Min);
        Assert.Equal(70, lengthRangePart.Max);
    }

    [Fact]
    public void GetRulePartsForProperty_StringLengthMinEqualsMax_ReturnsExactLengthRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Code",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "StringLength", Value = "8, MinimumLength = 8" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Code");

        ExactLengthRulePart exactLengthPart = Assert.Single(result.OfType<ExactLengthRulePart>());
        Assert.Equal(8, exactLengthPart.Length);
    }

    [Fact]
    public void GetRulePartsForProperty_Required_ReturnsNotEmptyRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Name",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "Required" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Name");

        Assert.Single(result.OfType<NotEmptyRulePart>());
    }

    [Fact]
    public void GetRulePartsForProperty_Range_ReturnsGreaterAndLessThanParts()
    {
        SpiderlyProperty property = new()
        {
            Name = "Quantity",
            Type = "int",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "Range", Value = "0, 100" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Quantity");

        GreaterThanOrEqualToRulePart gtePart = Assert.Single(result.OfType<GreaterThanOrEqualToRulePart>());
        Assert.Equal("0", gtePart.Value);

        LessThanOrEqualToRulePart ltePart = Assert.Single(result.OfType<LessThanOrEqualToRulePart>());
        Assert.Equal("100", ltePart.Value);
    }

    [Fact]
    public void GetRulePartsForProperty_Precision_ReturnsPrecisionScaleRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Price",
            Type = "decimal",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "Precision", Value = "18, 2" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Price");

        PrecisionScaleRulePart precisionPart = Assert.Single(result.OfType<PrecisionScaleRulePart>());
        Assert.Equal(18, precisionPart.Precision);
        Assert.Equal(2, precisionPart.Scale);
    }

    [Fact]
    public void GetRulePartsForProperty_Email_ReturnsEmailAddressRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Email",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "Required" }, new SpiderlyAttribute { Name = "Email" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Email");

        Assert.Single(result.OfType<EmailAddressRulePart>());
    }

    [Fact]
    public void GetRulePartsForProperty_OptionalStringLength_AddsUnlessRulePart()
    {
        SpiderlyProperty property = new()
        {
            Name = "Description",
            Type = "string",
            Attributes = [new SpiderlyAttribute { Name = "StringLength", Value = "500" }]
        };

        List<SpiderValidationRulePart> result = ValidationRuleBuilder.GetRulePartsForProperty(property, "Description");

        Assert.Single(result.OfType<MaximumLengthRulePart>());
        UnlessRulePart unlessPart = Assert.Single(result.OfType<UnlessRulePart>());
        Assert.Equal("i => string.IsNullOrEmpty(i.Description)", unlessPart.Condition);
    }

    #endregion

    #region ExtractTypeFromGenericType

    [Theory]
    [InlineData("List<long>", "long")]
    [InlineData("List<User>", "User")]
    [InlineData(null, null)]
    public void ExtractTypeFromGenericType_VariousInputs_ExtractsInnerType(string? input, string? expected)
    {
        Assert.Equal(expected, Helpers.ExtractTypeFromGenericType(input));
    }

    #endregion

    #region Static Properties

    [Fact]
    public void BusinessObject_DefaultValue_IsBusinessObject()
    {
        Assert.Equal("BusinessObject", Helpers.BusinessObject);
    }

    [Fact]
    public void ReadonlyObject_DefaultValue_IsReadonlyObject()
    {
        Assert.Equal("ReadonlyObject", Helpers.ReadonlyObject);
    }

    [Fact]
    public void DTONamespaceEnding_DefaultValue_IsDTO()
    {
        Assert.Equal("DTO", Helpers.DTONamespaceEnding);
    }

    #endregion
}
