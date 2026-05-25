using System.Collections.Generic;
using System.Threading.Tasks;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Snapshot of the redesigned bare {Entity}Fields fragment + its config class for the scalar controls.
// Asserts the new shape: no panel, no For{Entity} suffix, config-driven *ngIf, formGroup-bound controls,
// below{Prop} content slots. This is the target output the eventual switchover will route through.
public class NgFieldsComponentGeneratorTests
{
    private static SpiderlyProperty Prop(string name, string type) =>
        new() { Name = name, Type = type, EntityName = "Brand" };

    [Fact]
    public Task EmitsBareFragmentAndConfig_ScalarControls()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                Prop("Name", "string"),
                Prop("Price", "decimal"),
                Prop("IsActive", "bool?"),
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand);

        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model)
            + "\n\n"
            + NgFieldsComponentGenerator.BuildFieldsConfig(model);

        return Verify(output);
    }

    [Fact]
    public Task EmitsAutocompleteFragment()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Brand" },
                new() { Name = "Country", Type = "Country", EntityName = "Brand" },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand);

        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model)
            + "\n\n"
            + NgFieldsComponentGenerator.BuildFieldsConfig(model);

        return Verify(output);
    }

    [Fact]
    public Task EmitsDropdownFragment()
    {
        SpiderlyClass brand = new()
        {
            Name = "Brand",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Brand" },
                new() { Name = "Status", Type = "BrandStatusCodes", EntityName = "Brand", IsEnum = true },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand);

        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model)
            + "\n\n"
            + NgFieldsComponentGenerator.BuildFieldsConfig(model);

        return Verify(output);
    }
}
