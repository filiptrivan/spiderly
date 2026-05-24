using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Tests.Generators;

// IsIncludedInDetailsUi is the single source of truth every details-UI sub-generator now gates on
// (field blocks, option vars, search methods, table cols/vars, ordered/complex panels). Testing the
// predicate proves all of those offenders honor [UIDoNotGenerate] by construction — the table-family
// and ordered/complex helpers previously enumerated entity.Properties raw and leaked artifacts.
public class IsIncludedInDetailsUiTests
{
    private static SpiderlyProperty Prop(string name, string type, params (string Name, string? Value)[] attributes) =>
        new()
        {
            Name = name,
            Type = type,
            Attributes = attributes.Select(a => new SpiderlyAttribute { Name = a.Name, Value = a.Value }).ToList(),
        };

    private static SpiderlyClass EntityWith(SpiderlyProperty property) =>
        new() { Name = "Article", Properties = new List<SpiderlyProperty> { property } };

    // Each collection control type that funnels through one of the (formerly leaky) generators.
    public static IEnumerable<object[]> CollectionControlAttributes()
    {
        yield return new object[] { "SimpleManyToManyTableLazyLoad", null! };
        yield return new object[] { "ComplexManyToManyReadonlyTable", null! };
        yield return new object[] { "ComplexManyToManyList", null! };
        yield return new object[] { "UIOrderedOneToMany", null! };
        yield return new object[] { "UIControlType", "MultiSelect" };
        yield return new object[] { "UIControlType", "MultiAutocomplete" };
    }

    [Theory]
    [MemberData(nameof(CollectionControlAttributes))]
    public void CollectionControl_WithUIDoNotGenerate_IsExcluded(string attributeName, string attributeValue)
    {
        SpiderlyProperty property = Prop("Tags", "List<Tag>",
            (attributeName, attributeValue),
            ("UIDoNotGenerate", null));

        Assert.False(property.IsIncludedInDetailsUi(EntityWith(property)));
    }

    [Theory]
    [MemberData(nameof(CollectionControlAttributes))]
    public void CollectionControl_WithoutUIDoNotGenerate_IsIncluded(string attributeName, string attributeValue)
    {
        SpiderlyProperty property = Prop("Tags", "List<Tag>",
            (attributeName, attributeValue));

        Assert.True(property.IsIncludedInDetailsUi(EntityWith(property)));
    }

    // [ExcludeFromDTO] removes the backing SaveBody/MainUIForm DTO field, so the UI gate must drop
    // the property too — otherwise the Angular generators emit getControl('selected{P}Ids') /
    // controls.selected{P}Ids.setValue(...) against a control initFormGroup never creates. Because
    // every details-UI sub-generator now gates on this predicate, asserting false here proves none
    // of them bind to the excluded property.
    [Theory]
    [MemberData(nameof(CollectionControlAttributes))]
    public void CollectionControl_WithExcludeFromDTO_IsExcluded(string attributeName, string attributeValue)
    {
        SpiderlyProperty property = Prop("Tags", "List<Tag>",
            (attributeName, attributeValue),
            ("ExcludeFromDTO", null));

        Assert.False(property.IsIncludedInDetailsUi(EntityWith(property)));
    }

    [Fact]
    public void Scalar_IsIncluded()
    {
        SpiderlyProperty property = Prop("Title", "string");

        Assert.True(property.IsIncludedInDetailsUi(EntityWith(property)));
    }

    [Theory]
    [InlineData("Id")]
    [InlineData("Version")]
    [InlineData("CreatedAt")]
    [InlineData("ModifiedAt")]
    public void FrameworkColumn_IsExcluded(string name)
    {
        SpiderlyProperty property = Prop(name, "long");

        Assert.False(property.IsIncludedInDetailsUi(EntityWith(property)));
    }

    [Fact]
    public void PlainOneToMany_WithoutSpecialControl_IsExcluded()
    {
        // A bare collection navigation (no multiselect/table/ordered/complex attribute) never renders.
        SpiderlyProperty property = Prop("ApiKeys", "List<ApiKey>");

        Assert.False(property.IsIncludedInDetailsUi(EntityWith(property)));
    }
}
