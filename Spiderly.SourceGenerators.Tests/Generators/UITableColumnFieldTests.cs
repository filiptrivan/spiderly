using System.Collections.Generic;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using Xunit;

namespace Spiderly.SourceGenerators.Tests.Generators;

/// <summary>
/// <c>[UITableColumn("Field")]</c> carries a hand-authored property name that nothing checks against the
/// entity or its DTO. A typo — or a rename that missed the attribute string — resolved to null and NRE'd
/// during generation. A misspelled column name is ordinary user error and deserves to say so.
/// </summary>
public class UITableColumnFieldTests
{
    [Fact]
    public void UnknownColumnField_YieldsALocatedDiagnostic()
    {
        SpiderlyGenerationException exception = Assert.Throws<SpiderlyGenerationException>(
            () => Resolve(columnField: "NoSuchColumn"));

        Assert.Equal("SPIDERLY026", exception.Diagnostic.Id);
        Assert.Contains("NoSuchColumn", exception.Diagnostic.GetMessage());
        Assert.Contains("Tag", exception.Diagnostic.GetMessage());
    }

    [Fact]
    public void UnknownNavigationTarget_YieldsALocatedDiagnostic()
    {
        // The other half of the same line: the target entity itself may not resolve.
        SpiderlyGenerationException exception = Assert.Throws<SpiderlyGenerationException>(
            () => Resolve(columnField: "Name", entities: new List<SpiderlyClass>()));

        Assert.Equal("SPIDERLY026", exception.Diagnostic.Id);
    }

    [Fact]
    public void KnownColumnField_Resolves()
    {
        List<string> columns = Resolve(columnField: "Name");

        Assert.Contains("field: 'name'", Assert.Single(columns));
    }

    private static List<string> Resolve(string columnField, List<SpiderlyClass>? entities = null)
    {
        SpiderlyClass tag = new()
        {
            Name = "Tag",
            Namespace = "Test.Entities",
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Id", Type = "long" },
                new() { Name = "Name", Type = "string" },
            },
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        };

        SpiderlyProperty tagsProperty = new()
        {
            Name = "Tags",
            Type = "List<Tag>",
            EntityName = "Product",
            Attributes = new List<SpiderlyAttribute>
            {
                new() { Name = "UITableColumn", Value = columnField },
            },
        };

        SpiderlyClass product = new()
        {
            Name = "Product",
            Namespace = "Test.Entities",
            Properties = new List<SpiderlyProperty> { tagsProperty },
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        };

        return NgDetailsDataGenerator.GetSimpleManyToManyTableLazyLoadCols(
            tagsProperty,
            product,
            entities ?? new List<SpiderlyClass> { tag, product },
            new List<SpiderlyClass>());
    }
}
