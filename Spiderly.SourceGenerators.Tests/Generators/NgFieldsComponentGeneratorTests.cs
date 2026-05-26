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

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());

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

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());

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

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());

        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model)
            + "\n\n"
            + NgFieldsComponentGenerator.BuildFieldsConfig(model);

        return Verify(output);
    }

    [Fact]
    public Task EmitsMultiSelectFragment()
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
                new()
                {
                    Name = "Tags", Type = "List<Tag>", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "MultiSelect" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsMultiAutocompleteFragment()
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
                new()
                {
                    Name = "Authors", Type = "List<Author>", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "MultiAutocomplete" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsCalendarFragment()
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
                new() { Name = "PublishedAt", Type = "DateTime", EntityName = "Brand" },
                new() { Name = "BirthDate", Type = "DateOnly", EntityName = "Brand" },
                new() { Name = "OpenTime", Type = "TimeOnly", EntityName = "Brand" },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsColorPickerFragment()
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
                new()
                {
                    Name = "Color", Type = "string", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "ColorPicker" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsEditorFragment()
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
                new()
                {
                    Name = "Content", Type = "string", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "Editor" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsEditorS3Fragment()
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
                new()
                {
                    Name = "Bio", Type = "string", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "UIControlType", Value = "Editor" },
                        new() { Name = "S3PublicStorage" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsFileFragment()
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
                new()
                {
                    Name = "Photo", Type = "string", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "File" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsFileFragmentWithImageConstraints()
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
                new()
                {
                    Name = "Photo", Type = "string", EntityName = "Brand",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "UIControlType", Value = "File" },
                        new() { Name = "ImageWidth", Value = "100" },
                        new() { Name = "ImageHeight", Value = "80" },
                        new() { Name = "AcceptedFileTypes", Value = ".jpg,.png" },
                        new() { Name = "MaxFileSize", Value = "5000" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(brand, new() { brand }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsBackReferenceGuardFragment()
    {
        SpiderlyClass segmentationItem = new()
        {
            Name = "SegmentationItem",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "SegmentationItem" },
                new()
                {
                    Name = "Segmentation", Type = "Segmentation", EntityName = "SegmentationItem",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "WithMany", Value = "SegmentationItems" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(segmentationItem, new() { segmentationItem }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsOrderedOneToManyComposition()
    {
        SpiderlyClass segmentation = new()
        {
            Name = "Segmentation",
            Namespace = "TestApp.Business.Entities",
            BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Segmentation" },
                new()
                {
                    Name = "SegmentationItems", Type = "List<SegmentationItem>", EntityName = "Segmentation",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIOrderedOneToMany" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(segmentation, new() { segmentation }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    [Fact]
    public Task EmitsComplexReadonlyTable()
    {
        SpiderlyClass role = new()
        {
            Name = "Role", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Role" } },
        };
        SpiderlyClass user = new()
        {
            Name = "User", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Email", Type = "string", EntityName = "User" },
                new()
                {
                    Name = "Roles", Type = "List<Role>", EntityName = "User",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "ComplexManyToManyReadonlyTable" },
                        new() { Name = "UITableColumn", Value = "Name" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(user, new() { user, role }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }
}
