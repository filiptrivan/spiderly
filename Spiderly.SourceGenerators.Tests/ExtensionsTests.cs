using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class ExtensionsTests
{
    #region IsBaseDataType

    [Theory]
    [InlineData("string")]
    [InlineData("bool")]
    [InlineData("bool?")]
    [InlineData("DateTime")]
    [InlineData("DateTime?")]
    [InlineData("System.DateTime")]
    [InlineData("System.DateTime?")]
    [InlineData("long")]
    [InlineData("long?")]
    [InlineData("int")]
    [InlineData("int?")]
    [InlineData("decimal")]
    [InlineData("decimal?")]
    [InlineData("float")]
    [InlineData("float?")]
    [InlineData("double")]
    [InlineData("double?")]
    [InlineData("byte")]
    [InlineData("byte?")]
    [InlineData("System.Guid")]
    [InlineData("System.Guid?")]
    [InlineData("Guid")]
    [InlineData("Guid?")]
    public void IsBaseDataType_BaseTypes_ReturnsTrue(string type)
    {
        Assert.True(type.IsBaseDataType());
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Product")]
    [InlineData("List<User>")]
    [InlineData("StatusCodes")]
    public void IsBaseDataType_NonBaseTypes_ReturnsFalse(string type)
    {
        Assert.False(type.IsBaseDataType());
    }

    #endregion

    #region IsManyToOneType

    [Theory]
    [InlineData("User")]
    [InlineData("Product")]
    [InlineData("Category")]
    public void IsManyToOneType_EntityTypes_ReturnsTrue(string type)
    {
        Assert.True(type.IsManyToOneType());
    }

    [Theory]
    [InlineData("string")]
    [InlineData("int")]
    [InlineData("bool?")]
    [InlineData("List<User>")]
    [InlineData("List<long>")]
    public void IsManyToOneType_BaseOrEnumerableTypes_ReturnsFalse(string type)
    {
        Assert.False(type.IsManyToOneType());
    }

    #endregion

    #region IsOneToManyType

    [Theory]
    [InlineData("List<User>")]
    [InlineData("List<Product>")]
    public void IsOneToManyType_EntityLists_ReturnsTrue(string type)
    {
        Assert.True(type.IsOneToManyType());
    }

    [Theory]
    [InlineData("List<long>")]
    [InlineData("List<string>")]
    [InlineData("User")]
    [InlineData("string")]
    public void IsOneToManyType_NonEntityOrNonList_ReturnsFalse(string type)
    {
        Assert.False(type.IsOneToManyType());
    }

    #endregion

    #region IsEnumerable

    [Theory]
    [InlineData("List<User>")]
    [InlineData("IList<User>")]
    [InlineData("User[]")]
    public void IsEnumerable_EnumerableTypes_ReturnsTrue(string type)
    {
        Assert.True(type.IsEnumerable());
    }

    [Theory]
    [InlineData("User")]
    [InlineData("string")]
    [InlineData("int")]
    public void IsEnumerable_NonEnumerableTypes_ReturnsFalse(string type)
    {
        Assert.False(type.IsEnumerable());
    }

    #endregion

    #region IsEnum

    [Theory]
    [InlineData("StatusCodes")]
    [InlineData("UIControlTypeCodes")]
    [InlineData("List<StatusCodes>")]
    public void IsEnum_EnumTypes_ReturnsTrue(string type)
    {
        Assert.True(type.IsEnum());
    }

    [Theory]
    [InlineData("User")]
    [InlineData("string")]
    [InlineData("Code")]
    public void IsEnum_NonEnumTypes_ReturnsFalse(string type)
    {
        Assert.False(type.IsEnum());
    }

    #endregion

    #region IsManyToMany

    [Fact]
    public void IsManyToMany_NullBaseType_ReturnsTrue()
    {
        SpiderlyClass c = new() { BaseType = null };
        Assert.True(c.IsManyToMany());
    }

    [Fact]
    public void IsManyToMany_WithBaseType_ReturnsFalse()
    {
        SpiderlyClass c = new() { BaseType = "BusinessObject<long>" };
        Assert.False(c.IsManyToMany());
    }

    #endregion

    #region IsBusinessObject / IsReadonlyObject

    [Fact]
    public void IsBusinessObject_BusinessObjectBase_ReturnsTrue()
    {
        SpiderlyClass c = new() { BaseType = "BusinessObject<long>" };
        Assert.True(c.IsBusinessObject());
    }

    [Fact]
    public void IsBusinessObject_ReadonlyObjectBase_ReturnsFalse()
    {
        SpiderlyClass c = new() { BaseType = "ReadonlyObject<long>" };
        Assert.False(c.IsBusinessObject());
    }

    [Fact]
    public void IsReadonlyObject_ReadonlyObjectBase_ReturnsTrue()
    {
        SpiderlyClass c = new() { BaseType = "ReadonlyObject<int>" };
        Assert.True(c.IsReadonlyObject());
    }

    [Fact]
    public void IsReadonlyObject_BusinessObjectBase_ReturnsFalse()
    {
        SpiderlyClass c = new() { BaseType = "BusinessObject<long>" };
        Assert.False(c.IsReadonlyObject());
    }

    #endregion

    #region FirstCharToUpper / FirstCharToLower

    [Theory]
    [InlineData("hello", "Hello")]
    [InlineData("Hello", "Hello")]
    [InlineData("a", "A")]
    public void FirstCharToUpper_NormalInput_ReturnsUppercased(string input, string expected)
    {
        Assert.Equal(expected, input.FirstCharToUpper());
    }

    [Fact]
    public void FirstCharToUpper_Null_ReturnsNull()
    {
        Assert.Null(((string?)null).FirstCharToUpper());
    }

    [Fact]
    public void FirstCharToUpper_Empty_ReturnsNull()
    {
        Assert.Null("".FirstCharToUpper());
    }

    [Theory]
    [InlineData("Hello", "hello")]
    [InlineData("hello", "hello")]
    [InlineData("A", "a")]
    public void FirstCharToLower_NormalInput_ReturnsLowercased(string input, string expected)
    {
        Assert.Equal(expected, input.FirstCharToLower());
    }

    [Fact]
    public void FirstCharToLower_Null_ReturnsNull()
    {
        Assert.Null(((string?)null).FirstCharToLower());
    }

    [Fact]
    public void FirstCharToLower_Empty_ReturnsNull()
    {
        Assert.Null("".FirstCharToLower());
    }

    #endregion

    #region FromPascalToKebabCase

    [Theory]
    [InlineData("UserProfile", "user-profile")]
    [InlineData("User", "user")]
    [InlineData("HTMLParser", "htmlparser")]
    [InlineData("", "")]
    public void FromPascalToKebabCase_VariousInputs_ReturnsKebab(string input, string expected)
    {
        Assert.Equal(expected, input.FromPascalToKebabCase());
    }

    #endregion

    #region SplitCamelCase

    [Theory]
    [InlineData("UserProfile", "User Profile")]
    [InlineData("User", "User")]
    [InlineData("HTMLParser", "HTML Parser")]
    public void SplitCamelCase_VariousInputs_ReturnsSplit(string input, string expected)
    {
        Assert.Equal(expected, input.SplitCamelCase());
    }

    [Fact]
    public void SplitCamelCase_Null_ReturnsNull()
    {
        Assert.Null(((string?)null).SplitCamelCase());
    }

    [Fact]
    public void SplitCamelCase_Empty_ReturnsEmpty()
    {
        Assert.Equal("", "".SplitCamelCase());
    }

    #endregion

    #region GetDTOBaseType

    [Fact]
    public void GetDTOBaseType_GenericBase_InsertsDTO()
    {
        SpiderlyClass c = new() { BaseType = "BusinessObject<long>" };
        Assert.Equal("BusinessObjectDTO<long>", c.GetDTOBaseType());
    }

    [Fact]
    public void GetDTOBaseType_NonGenericBase_AppendsDTO()
    {
        SpiderlyClass c = new() { BaseType = "CustomBase" };
        Assert.Equal("CustomBaseDTO", c.GetDTOBaseType());
    }

    [Fact]
    public void GetDTOBaseType_NullBase_ReturnsNull()
    {
        SpiderlyClass c = new() { BaseType = null };
        Assert.Null(c.GetDTOBaseType());
    }

    #endregion

    #region ExtractTypeFromGenericType (public, on Helpers)

    [Theory]
    [InlineData("List<long>", "long")]
    [InlineData("List<User>", "User")]
    [InlineData("IList<string>", "string")]
    [InlineData(null, null)]
    public void ExtractTypeFromGenericType_VariousInputs_ReturnsInnerType(string? input, string? expected)
    {
        Assert.Equal(expected, Helpers.ExtractTypeFromGenericType(input));
    }

    #endregion
}
