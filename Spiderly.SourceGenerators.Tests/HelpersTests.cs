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
        Assert.Equal(expected, AngularTypeMapper.GetAngularType(cSharpType));
    }

    [Fact]
    public void GetAngularType_EnumType_ReturnsSameType()
    {
        Assert.Equal("StatusCodes", AngularTypeMapper.GetAngularType("StatusCodes"));
    }

    [Fact]
    public void GetAngularType_List_ReturnsArrayType()
    {
        string result = AngularTypeMapper.GetAngularType("List<long>");
        Assert.Equal("number[]", result);
    }

    [Fact]
    public void GetAngularType_ActionResult_ReturnsAny()
    {
        Assert.Equal("any", AngularTypeMapper.GetAngularType("ActionResult"));
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
