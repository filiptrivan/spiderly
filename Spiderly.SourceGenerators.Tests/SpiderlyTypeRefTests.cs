using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests;

public class SpiderlyTypeRefTests
{
    #region Raw / ToString round-trip

    [Theory]
    [InlineData("int")]
    [InlineData("int?")]
    [InlineData("List<Foo>")]
    [InlineData("List<Foo>?")]
    [InlineData("Foo[]")]
    [InlineData("List<NamebookDTO<long>>")]
    [InlineData("  List<Foo>  ")] // surrounding whitespace must survive verbatim for emission
    public void Raw_RoundTripsExactInput(string input)
    {
        SpiderlyTypeRef typeRef = SpiderlyTypeRef.Parse(input);
        Assert.Equal(input, typeRef.Raw);
        Assert.Equal(input, typeRef.ToString());
    }

    #endregion

    #region IsNullable

    [Theory]
    [InlineData("int?", true)]
    [InlineData("Foo?", true)]
    [InlineData("List<Foo>?", true)]
    [InlineData("int", false)]
    [InlineData("List<Foo>", false)]
    [InlineData("List<Foo?>", false)] // the ? belongs to the element, not the outer type
    public void IsNullable_DetectsOuterNullabilityOnly(string input, bool expected)
    {
        Assert.Equal(expected, SpiderlyTypeRef.Parse(input).IsNullable);
    }

    #endregion

    #region IsCollection

    [Theory]
    [InlineData("List<Foo>", true)]
    [InlineData("IList<Foo>", true)]
    [InlineData("ICollection<Foo>", true)]
    [InlineData("IEnumerable<Foo>", true)]
    [InlineData("Foo[]", true)]
    [InlineData("List<Foo>?", true)]
    [InlineData("Foo", false)]
    [InlineData("int?", false)]
    [InlineData("NamebookDTO<long>", false)] // a generic, but not a collection
    public void IsCollection_DetectsCollections(string input, bool expected)
    {
        Assert.Equal(expected, SpiderlyTypeRef.Parse(input).IsCollection);
    }

    #endregion

    #region Name (outer nominal)

    [Theory]
    [InlineData("List<Foo>", "List")]
    [InlineData("Foo?", "Foo")]
    [InlineData("Foo[]", "Foo")]
    [InlineData("int", "int")]
    [InlineData("NamebookDTO<long>", "NamebookDTO")]
    public void Name_ReturnsOuterNominalName(string input, string expected)
    {
        Assert.Equal(expected, SpiderlyTypeRef.Parse(input).Name);
    }

    #endregion

    #region CoreName (fully unwrapped underlying)

    [Theory]
    [InlineData("MyEnum", "MyEnum")]
    [InlineData("MyEnum?", "MyEnum")]
    [InlineData("List<MyEnum>", "MyEnum")] // the enum-import case: must NOT leak "List" or "?"
    [InlineData("List<MyEnum>?", "MyEnum")]
    [InlineData("MyEnum[]", "MyEnum")]
    [InlineData("List<NamebookDTO<long>>", "long")] // legacy ExtractTypeFromGenericType goes innermost
    [InlineData("int", "int")]
    public void CoreName_UnwrapsToInnermostName(string input, string expected)
    {
        Assert.Equal(expected, SpiderlyTypeRef.Parse(input).CoreName);
    }

    #endregion

    #region ElementType

    [Fact]
    public void ElementType_Collection_ReturnsElement()
    {
        Assert.Equal("Foo", SpiderlyTypeRef.Parse("List<Foo>").ElementType?.Name);
    }

    [Fact]
    public void ElementType_Array_ReturnsElement()
    {
        Assert.Equal("Foo", SpiderlyTypeRef.Parse("Foo[]").ElementType?.Name);
    }

    [Fact]
    public void ElementType_SimpleType_IsNull()
    {
        Assert.Null(SpiderlyTypeRef.Parse("int").ElementType);
    }

    #endregion

    #region Parse / conversion / equality edge cases

    [Fact]
    public void Parse_Null_ReturnsNull()
    {
        Assert.Null(SpiderlyTypeRef.Parse(null));
    }

    [Fact]
    public void ImplicitConversion_FromString_Parses()
    {
        SpiderlyTypeRef typeRef = "List<Foo>";
        Assert.Equal("List", typeRef.Name);
        Assert.True(typeRef.IsCollection);
    }

    [Fact]
    public void Equality_SameRaw_AreEqual()
    {
        Assert.Equal(SpiderlyTypeRef.Parse("List<Foo>"), SpiderlyTypeRef.Parse("List<Foo>"));
        Assert.Equal(SpiderlyTypeRef.Parse("List<Foo>").GetHashCode(), SpiderlyTypeRef.Parse("List<Foo>").GetHashCode());
    }

    [Fact]
    public void Equality_DifferentRaw_AreNotEqual()
    {
        Assert.NotEqual(SpiderlyTypeRef.Parse("Foo"), SpiderlyTypeRef.Parse("Foo?"));
    }

    #endregion
}
