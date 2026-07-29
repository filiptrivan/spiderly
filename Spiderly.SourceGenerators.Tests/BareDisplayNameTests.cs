using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using Xunit;

namespace Spiderly.SourceGenerators.Tests;

/// <summary>
/// <c>[DisplayName]</c>'s constructor argument is optional, so a bare <c>[DisplayName]</c> on an entity is
/// valid C# that compiles fine — and leaves the attribute's Value null. Both the validator whose job is to
/// turn bad <c>[DisplayName]</c> input into good diagnostics, and the consumption site that reads the path,
/// dereferenced that null.
/// </summary>
public class BareDisplayNameTests
{
    [Fact]
    public void ValidateDisplayNameAttributes_BareDisplayNameOnEntity_YieldsSPIDERLY025()
    {
        List<SpiderlyClass> entities = new() { EntityWithBareDisplayName() };

        Diagnostic[] diagnostics = Validations.ValidateDisplayNameAttributes(entities, entities).ToArray();

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("SPIDERLY025", diagnostic.Id);
        Assert.Contains("Order", diagnostic.GetMessage());
    }

    [Fact]
    public void ValidateDisplayNameAttributes_WithAPath_StillValidatesIt()
    {
        // The guard must not short-circuit the real path validation it precedes.
        SpiderlyClass order = new()
        {
            Name = "Order",
            Namespace = "Test.Entities",
            Properties = new List<SpiderlyProperty>(),
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "SpiderlyEntity" },
                new() { Name = "DisplayName", Value = "NoSuchProperty" },
            },
        };
        List<SpiderlyClass> entities = new() { order };

        Diagnostic diagnostic = Assert.Single(Validations.ValidateDisplayNameAttributes(entities, entities).ToArray());

        Assert.Equal("SPIDERLY007", diagnostic.Id);
    }

    [Fact]
    public void GetDisplayNameProperty_BareDisplayNameOnEntity_FallsBackToTheMarkedProperty()
    {
        // Not every generator runs the validator first, so faulting here would hand the user the opaque
        // failure instead of the good diagnostic. Degrade to the property-level lookup instead.
        SpiderlyClass order = EntityWithBareDisplayName();
        order.Properties.Add(new SpiderlyProperty
        {
            Name = "Code",
            Type = "string",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "DisplayName" } },
        });

        Assert.Equal("Code", ClassAnalyzer.GetDisplayNameProperty(order));
    }

    [Fact]
    public void GetDisplayNameProperty_BareDisplayNameAndNoMarkedProperty_FallsBackToId()
    {
        Assert.Equal("Id.ToString()", ClassAnalyzer.GetDisplayNameProperty(EntityWithBareDisplayName()));
    }

    private static SpiderlyClass EntityWithBareDisplayName() => new()
    {
        Name = "Order",
        Namespace = "Test.Entities",
        Properties = new List<SpiderlyProperty>(),
        Attributes = new List<SpiderlyAttribute>
        {
            new() { Name = "SpiderlyEntity" },
            // [DisplayName] with no argument — valid syntax, so Value is null.
            new() { Name = "DisplayName" },
        },
    };
}
