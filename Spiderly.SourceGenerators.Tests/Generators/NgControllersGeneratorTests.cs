using System;
using System.Collections.Generic;
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
}
