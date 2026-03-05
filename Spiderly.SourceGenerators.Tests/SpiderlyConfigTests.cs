using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests;

public class SpiderlyConfigTests
{
    #region Parse

    [Fact]
    public void Parse_ValidJson_ReturnsConfig()
    {
        string json = """{"generators": {"ServicesGenerator": false}}""";

        SpiderlyConfig config = SpiderlyConfig.Parse(json);

        Assert.NotNull(config);
        Assert.Single(config.Generators);
        Assert.False(config.Generators["ServicesGenerator"]);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsDefaultConfig()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse("");

        Assert.NotNull(config);
        Assert.Empty(config.Generators);
    }

    [Fact]
    public void Parse_Null_ReturnsDefaultConfig()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse(null!);

        Assert.NotNull(config);
        Assert.Empty(config.Generators);
    }

    [Fact]
    public void Parse_MalformedJson_ReturnsDefaultConfig()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse("{invalid json");

        Assert.NotNull(config);
        Assert.Empty(config.Generators);
    }

    [Fact]
    public void Parse_WithApiConfig_ReturnsRoutePrefix()
    {
        string json = """{"api": {"routePrefix": "/v2"}}""";

        SpiderlyConfig config = SpiderlyConfig.Parse(json);

        Assert.Equal("/v2", config.Api.RoutePrefix);
    }

    #endregion

    #region IsGeneratorEnabled

    [Fact]
    public void IsGeneratorEnabled_ConfiguredTrue_ReturnsTrue()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse("""{"generators": {"ServicesGenerator": true}}""");

        Assert.True(config.IsGeneratorEnabled("ServicesGenerator"));
    }

    [Fact]
    public void IsGeneratorEnabled_ConfiguredFalse_ReturnsFalse()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse("""{"generators": {"ServicesGenerator": false}}""");

        Assert.False(config.IsGeneratorEnabled("ServicesGenerator"));
    }

    [Fact]
    public void IsGeneratorEnabled_Unconfigured_ReturnsTrue()
    {
        SpiderlyConfig config = SpiderlyConfig.Parse("{}");

        Assert.True(config.IsGeneratorEnabled("SomeGenerator"));
    }

    #endregion

    #region Equality

    [Fact]
    public void Equals_SameGenerators_ReturnsTrue()
    {
        SpiderlyConfig a = SpiderlyConfig.Parse("""{"generators": {"A": true, "B": false}}""");
        SpiderlyConfig b = SpiderlyConfig.Parse("""{"generators": {"A": true, "B": false}}""");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Equals_DifferentGenerators_ReturnsFalse()
    {
        SpiderlyConfig a = SpiderlyConfig.Parse("""{"generators": {"A": true}}""");
        SpiderlyConfig b = SpiderlyConfig.Parse("""{"generators": {"A": false}}""");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_DifferentApiRoutePrefix_ReturnsFalse()
    {
        SpiderlyConfig a = SpiderlyConfig.Parse("""{"api": {"routePrefix": "/api"}}""");
        SpiderlyConfig b = SpiderlyConfig.Parse("""{"api": {"routePrefix": "/v2"}}""");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_Null_ReturnsFalse()
    {
        SpiderlyConfig config = new();

        Assert.False(config.Equals(null));
    }

    [Fact]
    public void Equals_SameReference_ReturnsTrue()
    {
        SpiderlyConfig config = new();

        Assert.True(config.Equals(config));
    }

    [Fact]
    public void GetHashCode_SameConfigs_SameHash()
    {
        SpiderlyConfig a = SpiderlyConfig.Parse("""{"generators": {"A": true}}""");
        SpiderlyConfig b = SpiderlyConfig.Parse("""{"generators": {"A": true}}""");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    #endregion
}
