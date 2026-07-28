using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests;

public class SpiderlyNamingTests
{
    #region IsGeneratedDTOName (default suffixes)

    [Theory]
    [InlineData("UserDTO", "User", true)]
    [InlineData("UserSaveBodyDTO", "User", true)]
    [InlineData("UserMainUIFormDTO", "User", true)]
    [InlineData("UserDTO", "Order", false)]
    // The bug this locks: an entity whose own name contains "DTO" mid-string. Build-forward-and-compare
    // (this method) never mismatches it the way a strip-then-compare idiom (`.Replace("DTO","")`) would.
    [InlineData("ProductDTOResponseDTO", "ProductDTOResponse", true)]
    [InlineData("ProductDTOResponseSaveBodyDTO", "ProductDTOResponse", true)]
    [InlineData("ProductDTOResponseDTO", "ProductResponse", false)]
    public void IsGeneratedDTOName_DefaultSuffixes_MatchesExactGeneratedName(string dtoClassName, string entityName, bool expected)
    {
        Assert.Equal(expected, SpiderlyNaming.IsGeneratedDTOName(dtoClassName, entityName));
    }

    #endregion

    #region IsGeneratedDTOName (explicit suffix subset)

    [Theory]
    [InlineData("ProductDTOResponseDTO", "ProductDTOResponse", true)]
    [InlineData("ProductDTOResponseSaveBodyDTO", "ProductDTOResponse", true)]
    // MainUIFormDTO is deliberately excluded from the suffix list passed in, so it must not match
    // even though it's one of SpiderlyNaming.DTOSuffixes.
    [InlineData("ProductDTOResponseMainUIFormDTO", "ProductDTOResponse", false)]
    public void IsGeneratedDTOName_ExplicitSuffixSubset_OnlyMatchesPassedSuffixes(string dtoClassName, string entityName, bool expected)
    {
        Assert.Equal(expected, SpiderlyNaming.IsGeneratedDTOName(dtoClassName, entityName, "DTO", "SaveBodyDTO"));
    }

    [Theory]
    [InlineData("ProductDTOResponseDTO", "ProductDTOResponse", true)]
    [InlineData("ProductDTOResponseSaveBodyDTO", "ProductDTOResponse", false)]
    public void IsGeneratedDTOName_SingleSuffix_OnlyMatchesThatSuffix(string dtoClassName, string entityName, bool expected)
    {
        Assert.Equal(expected, SpiderlyNaming.IsGeneratedDTOName(dtoClassName, entityName, "DTO"));
    }

    #endregion
}
