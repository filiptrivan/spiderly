using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class PrimaryKeyDiagnosticTests
{
    private static SpiderlyClass MakeEntity(string name, string baseType) => new()
    {
        Name = name,
        Namespace = "TestApp.Entities",
        BaseType = baseType,
    };

    [Theory]
    [InlineData("BusinessObject<Guid>", "Guid")]
    [InlineData("BusinessObject<decimal>", "decimal")]
    [InlineData("BusinessObject<short>", "short")]
    [InlineData("BusinessObject<DateTime>", "DateTime")]
    [InlineData("ReadonlyObject<Guid>", "Guid")]
    public void GetIdType_DisallowedPrimaryKey_ThrowsSPIDERLY018(string baseType, string badIdType)
    {
        SpiderlyClass entity = MakeEntity("Foo", baseType);

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => entity.GetIdType(new List<SpiderlyClass>()));

        Assert.Equal("SPIDERLY018", ex.Diagnostic.Id);
        Assert.Contains($"<{badIdType}>", ex.Diagnostic.GetMessage());
        Assert.Contains("must be int, long, or byte", ex.Diagnostic.GetMessage());
    }

    [Theory]
    [InlineData("BusinessObject<int>", "int")]
    [InlineData("BusinessObject<long>", "long")]
    [InlineData("BusinessObject<byte>", "byte")]
    [InlineData("ReadonlyObject<int>", "int")]
    [InlineData("ReadonlyObject<long>", "long")]
    [InlineData("ReadonlyObject<byte>", "byte")]
    public void GetIdType_AllowedPrimaryKey_ReturnsType(string baseType, string expectedIdType)
    {
        SpiderlyClass entity = MakeEntity("Foo", baseType);

        string idType = entity.GetIdType(new List<SpiderlyClass>());

        Assert.Equal(expectedIdType, idType);
    }

    [Fact]
    public void GetIdType_TransitiveBase_StillValidatesIdType()
    {
        SpiderlyClass intermediate = MakeEntity("Intermediate", "BusinessObject<Guid>");
        SpiderlyClass entity = MakeEntity("Foo", "Intermediate");

        SpiderlyGenerationException ex = Assert.Throws<SpiderlyGenerationException>(
            () => entity.GetIdType(new List<SpiderlyClass> { intermediate }));

        Assert.Equal("SPIDERLY018", ex.Diagnostic.Id);
    }
}
