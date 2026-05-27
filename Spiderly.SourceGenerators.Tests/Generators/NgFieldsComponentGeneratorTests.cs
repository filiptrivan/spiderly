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

    [Fact]
    public Task EmitsSimpleLazyLoadTable()
    {
        SpiderlyClass permission = new()
        {
            Name = "Permission", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Permission" } },
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
                    Name = "Permissions", Type = "List<Permission>", EntityName = "User",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "SimpleManyToManyTableLazyLoad" },
                        new() { Name = "UITableColumn", Value = "Name" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(user, new() { user, permission }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // [UISection] grouping: the fragment owns stacked panels (one per section + a headerless one for
    // unsectioned blocks), with isFirst/isMiddle/isLast chrome and [before]/[after] in the first/last panels.
    [Fact]
    public Task EmitsSectionedFragment()
    {
        SpiderlyClass account = new()
        {
            Name = "Account", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Account", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "General" } } },
                new() { Name = "Note", Type = "string", EntityName = "Account" },
                new() { Name = "Code", Type = "string", EntityName = "Account", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "General" } } },
                new() { Name = "Secret", Type = "string", EntityName = "Account", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "Security" } } },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(account, new() { account }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // A single named [UISection] with no unsectioned blocks: one panel (isOnly), all multi-panel flags false,
    // header shown — confirms a single section still triggers grouped mode (not the flat path).
    [Fact]
    public Task EmitsSingleSectionFragment()
    {
        SpiderlyClass account = new()
        {
            Name = "Account", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Account", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "General" } } },
                new() { Name = "Code", Type = "string", EntityName = "Account", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "General" } } },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(account, new() { account }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // [ComplexManyToManyList]: a collapsible card panel, one index-card per junction row, header = the related
    // (other-side) entity's display name, junction payload fields rendered inline. No CRUD menu / add-button.
    [Fact]
    public Task EmitsComplexManyToManyListPanel()
    {
        SpiderlyClass warehouse = new()
        {
            Name = "Warehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Warehouse" } },
        };
        SpiderlyClass junction = new()
        {
            Name = "ProductVariantWarehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" }, new() { Name = "M2M" } },
            Properties = new List<SpiderlyProperty>
            {
                new()
                {
                    Name = "ProductVariant", Type = "ProductVariant", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } },
                },
                new()
                {
                    Name = "Warehouse", Type = "Warehouse", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } },
                },
                new() { Name = "Stock", Type = "int", EntityName = "ProductVariantWarehouse" },
            },
        };
        SpiderlyClass productVariant = new()
        {
            Name = "ProductVariant", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "ProductVariant" },
                new()
                {
                    Name = "ProductVariantWarehouses", Type = "List<ProductVariantWarehouse>", EntityName = "ProductVariant",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "ComplexManyToManyList" } },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(productVariant, new() { productVariant, junction, warehouse }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // An ordered-O2M child with File controls: the parent fragment re-exposes each as its own @Output and wires the
    // child fragment's on{Prop}Uploaded in the @for row to re-emit { event, formGroup } with the row's DTO group.
    [Fact]
    public Task EmitsOrderedOneToManyFileOutputs()
    {
        SpiderlyClass media = new()
        {
            Name = "ProductMedia", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Alt", Type = "string", EntityName = "ProductMedia" },
                new() { Name = "Url", Type = "string", EntityName = "ProductMedia",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "File" } } },
                new() { Name = "ThumbnailUrl", Type = "string", EntityName = "ProductMedia",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIControlType", Value = "File" } } },
            },
        };
        SpiderlyClass product = new()
        {
            Name = "Product", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "Product" },
                new() { Name = "ProductMedia", Type = "List<ProductMedia>", EntityName = "Product",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "UIOrderedOneToMany" } } },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(product, new() { product, media }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // A [ComplexManyToManyList] property carrying a [UISection] renders inside that section's panel (grouped mode).
    [Fact]
    public Task EmitsComplexManyToManyListInSection()
    {
        SpiderlyClass warehouse = new()
        {
            Name = "Warehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Warehouse" } },
        };
        SpiderlyClass junction = new()
        {
            Name = "ProductVariantWarehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" }, new() { Name = "M2M" } },
            Properties = new List<SpiderlyProperty>
            {
                new()
                {
                    Name = "ProductVariant", Type = "ProductVariant", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } },
                },
                new()
                {
                    Name = "Warehouse", Type = "Warehouse", EntityName = "ProductVariantWarehouse",
                    Attributes = new List<SpiderlyAttribute> { new() { Name = "M2MWithMany", Value = "ProductVariantWarehouses" } },
                },
                new() { Name = "Stock", Type = "int", EntityName = "ProductVariantWarehouse" },
            },
        };
        SpiderlyClass productVariant = new()
        {
            Name = "ProductVariant", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty>
            {
                new() { Name = "Name", Type = "string", EntityName = "ProductVariant", Attributes = new List<SpiderlyAttribute> { new() { Name = "UISection", Value = "General" } } },
                new()
                {
                    Name = "ProductVariantWarehouses", Type = "List<ProductVariantWarehouse>", EntityName = "ProductVariant",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "ComplexManyToManyList" },
                        new() { Name = "UISection", Value = "Stock" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(productVariant, new() { productVariant, junction, warehouse }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }

    // A single entity with BOTH a readonly and an editable table: cols-init covers both; selection fields,
    // handler methods, and the form wiring appear ONLY for the editable one.
    [Fact]
    public Task EmitsReadonlyAndEditableTablesTogether()
    {
        SpiderlyClass role = new()
        {
            Name = "Role", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Role" } },
        };
        SpiderlyClass permission = new()
        {
            Name = "Permission", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
            Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
            Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Permission" } },
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
                new()
                {
                    Name = "Permissions", Type = "List<Permission>", EntityName = "User",
                    Attributes = new List<SpiderlyAttribute>
                    {
                        new() { Name = "SimpleManyToManyTableLazyLoad" },
                        new() { Name = "UITableColumn", Value = "Name" },
                    },
                },
            },
        };

        FieldsComponentModel model = NgFieldsModelBuilder.Build(user, new() { user, role, permission }, new());
        string output = NgFieldsComponentGenerator.BuildFieldsComponent(model) + "\n\n" + NgFieldsComponentGenerator.BuildFieldsConfig(model);
        return Verify(output);
    }
}
