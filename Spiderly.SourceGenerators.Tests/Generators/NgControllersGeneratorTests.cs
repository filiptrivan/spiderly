using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

public class NgControllersGeneratorTests
{
    private static SpiderlyMethod Method(string name, params string[] attributeNames) => new()
    {
        Name = name,
        Attributes = new List<SpiderlyAttribute>(
            Array.ConvertAll(attributeNames, a => new SpiderlyAttribute { Name = a })),
    };

    private static SpiderlyMethod MethodWithReturnType(string returnType, params string[] attributeNames) => new()
    {
        Name = "TestMethod",
        ReturnType = returnType,
        Attributes = new List<SpiderlyAttribute>(
            Array.ConvertAll(attributeNames, a => new SpiderlyAttribute { Name = a })),
    };

    /// <summary>
    /// Locks the bug this shipped with: a [NonAction] override (a consumer suppressing a
    /// generated base action) carries no Http* attribute, so the emission loop reached
    /// GetHttpType, threw, and the WHOLE api.service.generated.ts silently stopped
    /// regenerating for that project.
    /// </summary>
    [Fact]
    public void IsEndpointMethod_NonActionAndUIDoNotGenerate_AreSkipped()
    {
        Assert.False(NgControllersGenerator.IsEndpointMethod(Method("SaveProductVariant", "NonAction")));
        Assert.False(NgControllersGenerator.IsEndpointMethod(Method("Hidden", "UIDoNotGenerate")));
        Assert.True(NgControllersGenerator.IsEndpointMethod(Method("GetOrders", "HttpGet", "AuthGuard")));
        Assert.True(NgControllersGenerator.IsEndpointMethod(Method("NoAttributesYet")));
    }

    // A real endpoint with a missing Http* attribute is still an error — but the message must
    // name the offender (the anonymous "Http type doesn't exist." turned a one-line fix into a
    // repo-wide hunt).
    [Fact]
    public void GetHttpType_MissingHttpAttribute_NamesTheOffendingAction()
    {
        NotImplementedException ex = Assert.Throws<NotImplementedException>(
            () => NgControllersGenerator.GetHttpType(Method("OrderByToken", "AuthGuard")));

        Assert.Contains("OrderByToken", ex.Message);
        Assert.Contains("NonAction", ex.Message); // the escape hatch is named too
    }

    /// <summary>
    /// Locks the bug this shipped with (the same class the /simplify review already fixed once in
    /// ValidateControllerType, see AngularTypeDispatchCharacterizationTests): the read-shaped-DTO skip
    /// list sniffed the raw return-type string with <c>.Contains(...)</c>, so a user DTO that merely
    /// CONTAINS a framework read-shape name (e.g. "BrandNamebookDTO") was misclassified as
    /// "read-shaped" and silently opted out of the spinner. The exact framework type must still skip it.
    /// </summary>
    [Fact]
    public void ShouldSkipSpinner_ReturnTypeMerelyContainsReadShapedDtoName_DoesNotSkip()
    {
        Assert.False(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("BrandNamebookDTO"), ImmutableArray<string>.Empty));
        Assert.False(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("ProductPaginatedResultDTO"), ImmutableArray<string>.Empty));
    }

    [Fact]
    public void ShouldSkipSpinner_ExactReadShapedDtoReturnType_Skips()
    {
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("NamebookDTO<long>"), ImmutableArray<string>.Empty));
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("CodebookDTO<long>"), ImmutableArray<string>.Empty));
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("LazyLoadSelectedIdsResultDTO"), ImmutableArray<string>.Empty));
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("PaginatedResultDTO<UserDTO>"), ImmutableArray<string>.Empty));
    }

    // Transport-wrapper/collection nesting must still unwrap down to the read-shaped DTO, matching what
    // the old Contains() sniffing caught for free by scanning the whole raw string.
    [Fact]
    public void ShouldSkipSpinner_WrappedReadShapedDtoReturnType_Skips()
    {
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("Task<List<NamebookDTO<long>>>"), ImmutableArray<string>.Empty));
        Assert.True(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("ActionResult<PaginatedResultDTO<UserDTO>>"), ImmutableArray<string>.Empty));
    }

    [Fact]
    public void ShouldSkipSpinner_UnrelatedDtoReturnType_DoesNotSkip()
    {
        Assert.False(NgControllersGenerator.ShouldSkipSpinner(MethodWithReturnType("UserDTO"), ImmutableArray<string>.Empty));
    }
}
