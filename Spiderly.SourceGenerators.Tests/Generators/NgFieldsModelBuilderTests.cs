using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Models;

namespace Spiderly.SourceGenerators.Tests.Generators;

// Unit tests for the structured emission model builder. Validates per-field facts for scalar controls
// (TextBox, Integer, Decimal, CheckBox) and M2O Autocomplete controls.
public class NgFieldsModelBuilderTests
{
    private static SpiderlyProperty Prop(string name, string type, params (string Name, string? Value)[] attributes) =>
        new()
        {
            Name = name,
            Type = type,
            EntityName = "Brand",
            Attributes = attributes.Select(a => new SpiderlyAttribute { Name = a.Name, Value = a.Value }).ToList(),
        };

    private static SpiderlyClass Segmentation() => new()
    {
        Name = "Segmentation",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Name", "string"),
            new()
            {
                Name = "SegmentationItems", Type = "List<SegmentationItem>", EntityName = "Segmentation",
                Attributes = new List<SpiderlyAttribute> { new() { Name = "UIOrderedOneToMany" } },
            },
        },
    };

    private static SpiderlyClass Role() => new()
    {
        Name = "Role",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty> { Prop("Name", "string") },
    };

    private static SpiderlyClass User() => new()
    {
        Name = "User",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Email", "string"),
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

    private static SpiderlyClass Permission() => new()
    {
        Name = "Permission",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty> { Prop("Name", "string") },
    };

    private static SpiderlyClass UserWithEditableTable() => new()
    {
        Name = "User",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Email", "string"),
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

    private static SpiderlyClass SectionedEntity() => new()
    {
        Name = "Account",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Name", "string", ("UISection", "General")),
            Prop("Note", "string"),
            Prop("Code", "string", ("UISection", "General")),
            Prop("Secret", "string", ("UISection", "Security")),
        },
    };

    private static SpiderlyClass Warehouse() => new()
    {
        Name = "Warehouse", Namespace = "TestApp.Business.Entities", BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty> { new() { Name = "Name", Type = "string", EntityName = "Warehouse" } },
    };

    private static SpiderlyClass ProductVariantWarehouse() => new()
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

    private static SpiderlyClass ProductVariantWithWarehouses() => new()
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

    private static SpiderlyClass Brand() => new()
    {
        Name = "Brand",
        Namespace = "TestApp.Business.Entities",
        BaseType = "BusinessObject<long>",
        Attributes = new List<SpiderlyAttribute> { new() { Name = "SpiderlyEntity" } },
        Properties = new List<SpiderlyProperty>
        {
            Prop("Name", "string"),
            Prop("Price", "decimal"),
            Prop("Stock", "int"),
            Prop("IsActive", "bool?"),
            Prop("Country", "Country"),
            new() { Name = "Status", Type = "BrandStatusCodes", EntityName = "Brand", IsEnum = true },
            Prop("Description", "string", ("UIControlType", "TextArea")),
            Prop("Secret", "string", ("UIControlType", "Password")),
            Prop("Info", "string", ("UIControlType", "TextBlock")),
            Prop("Tags", "List<Tag>", ("UIControlType", "MultiSelect")),
            Prop("Authors", "List<Author>", ("UIControlType", "MultiAutocomplete")),
            Prop("PublishedAt", "DateTime"),
            Prop("BirthDate", "DateOnly"),
            Prop("OpenTime", "TimeOnly"),
            Prop("Color", "string", ("UIControlType", "ColorPicker")),
            Prop("Content", "string", ("UIControlType", "Editor")),
            Prop("Bio", "string", ("UIControlType", "Editor"), ("S3PublicStorage", null)),
            Prop("Photo", "string", ("UIControlType", "File")),
            Prop("ParentGroup", "Group", ("WithMany", "Brands")),
        },
    };

    [Fact]
    public void Build_SetsEntityLevelNames()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new());

        Assert.Equal("Brand", model.EntityName);
        Assert.Equal("brand-fields", model.Selector);
        Assert.Equal("BrandFieldsComponent", model.ComponentClassName);
        Assert.Equal("BrandSaveBody", model.SaveBodyTypeName);
        Assert.Equal("BrandFieldsConfig", model.ConfigClassName);
        Assert.Equal("formGroup.controls.brandDTO", model.MainDtoAccess);
    }

    [Fact]
    public void Build_TextBoxField_HasNoSuffixedNamesAndNoOutput()
    {
        FieldModel name = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Name");

        Assert.Equal("spiderly-textbox", name.ControlTag);
        Assert.Equal("name", name.FormControlName);
        Assert.Equal("showName", name.ConfigShowFlagName);
        Assert.Equal("", name.ExtraControlAttributes);
        Assert.Null(name.ChangeOutput);
    }

    [Fact]
    public void Build_DecimalField_AddsDecimalAttributes()
    {
        FieldModel price = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Price");

        Assert.Equal("spiderly-number", price.ControlTag);
        Assert.Equal("price", price.FormControlName);
        // Brand.Price has no [Precision] attribute, so the scale is empty. Assert the exact current shape so an
        // empty [maxFractionDigits] can't masquerade as a populated one (the builder passes GetDecimalScale through verbatim).
        Assert.Equal(" [decimal]=\"true\" [maxFractionDigits]=\"\"", price.ExtraControlAttributes);
    }

    [Fact]
    public void Build_IntegerField_HasNoExtraAttributes()
    {
        FieldModel stock = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Stock");

        Assert.Equal("spiderly-number", stock.ControlTag);
        Assert.Equal("stock", stock.FormControlName);
        Assert.Equal("", stock.ExtraControlAttributes);
        Assert.Null(stock.ChangeOutput);
    }

    [Fact]
    public void Build_CheckBoxField_HasChangeOutput()
    {
        FieldModel isActive = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "IsActive");

        Assert.Equal("spiderly-checkbox", isActive.ControlTag);
        Assert.Equal("isActive", isActive.FormControlName);
        Assert.NotNull(isActive.ChangeOutput);
        Assert.Equal("onIsActiveChange", isActive.ChangeOutput.OutputName);
        Assert.Equal("CheckboxChangeEvent", isActive.ChangeOutput.EventType);
        Assert.Equal("onChange", isActive.ChangeOutput.ControlEventName);
    }

    [Fact]
    public void Build_M2OAutocomplete_HasOptionsFieldAndSearch()
    {
        FieldModel country = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Country");

        Assert.Equal("spiderly-autocomplete", country.ControlTag);
        Assert.Equal("countryId", country.FormControlName);
        Assert.Equal("countryOptions", country.OptionsFieldName);
        Assert.False(country.OptionsIsInput);
        Assert.NotNull(country.Search);
        Assert.Equal("searchCountry", country.Search.MethodName);
        Assert.Equal("getCountryAutocompleteListForBrand", country.Search.ApiMethodName);
        Assert.Equal("countryOptions", country.Search.OptionsFieldName);
        Assert.Contains("[options]=\"countryOptions\"", country.ExtraControlAttributes);
        Assert.Contains("[displayName]=\"formGroup.controls.brandDTO.controls.countryDisplayName.getRawValue()\"", country.ExtraControlAttributes);
        Assert.Contains("(onTextInput)=\"searchCountry($event, formGroup.controls.brandDTO.controls.id.getRawValue())\"", country.ExtraControlAttributes);
    }

    [Fact]
    public void Build_Dropdown_HasInputOptionsAndChangeOutput()
    {
        FieldModel status = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Status");

        Assert.Equal("spiderly-dropdown", status.ControlTag);
        Assert.Equal("status", status.FormControlName);
        Assert.Equal("statusOptions", status.OptionsFieldName);
        Assert.True(status.OptionsIsInput);
        Assert.Contains("[options]=\"statusOptions\"", status.ExtraControlAttributes);
        Assert.NotNull(status.ChangeOutput);
        Assert.Equal("onStatusChange", status.ChangeOutput.OutputName);
        Assert.Equal("DropdownChangeEvent", status.ChangeOutput.EventType);
        Assert.Equal("onChange", status.ChangeOutput.ControlEventName);
    }

    [Theory]
    [InlineData("Description", "spiderly-textarea")]
    [InlineData("Secret", "spiderly-password")]
    [InlineData("Info", "spiderly-textblock")]
    public void Build_SimpleScalarControls_MapToTag(string propertyName, string expectedTag)
    {
        FieldModel field = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == propertyName);

        Assert.Equal(expectedTag, field.ControlTag);
        Assert.Equal("", field.ExtraControlAttributes);
        Assert.Null(field.ChangeOutput);
        Assert.Null(field.OptionsFieldName);
    }

    [Fact]
    public void Build_MultiSelect_BindsOnSaveBodyWithInputOptionsAndLabel()
    {
        FieldModel tags = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Tags");

        Assert.Equal("spiderly-multiselect", tags.ControlTag);
        Assert.Equal("selectedTagsIds", tags.FormControlName);
        Assert.True(tags.BindsOnSaveBody);
        Assert.Equal("tagsOptions", tags.OptionsFieldName);
        Assert.True(tags.OptionsIsInput);
        Assert.Null(tags.Search);
        Assert.Null(tags.ChangeOutput);
        Assert.Contains("[options]=\"tagsOptions\"", tags.ExtraControlAttributes);
        Assert.Contains("[label]=\"t('Tags')\"", tags.ExtraControlAttributes);
    }

    [Fact]
    public void Build_MultiAutocomplete_BindsOnSaveBodyWithSelfOwnedSearchAndLabel()
    {
        FieldModel authors = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Authors");

        Assert.Equal("spiderly-multiautocomplete", authors.ControlTag);
        Assert.Equal("selectedAuthorsNamebookDTOList", authors.FormControlName);
        Assert.True(authors.BindsOnSaveBody);
        Assert.Equal("authorsOptions", authors.OptionsFieldName);
        Assert.False(authors.OptionsIsInput);
        Assert.NotNull(authors.Search);
        Assert.Equal("searchAuthors", authors.Search.MethodName);
        Assert.Equal("getAuthorsAutocompleteListForBrand", authors.Search.ApiMethodName);
        Assert.Contains("[options]=\"authorsOptions\"", authors.ExtraControlAttributes);
        Assert.Contains("(onTextInput)=\"searchAuthors($event, formGroup.controls.brandDTO.controls.id.getRawValue())\"", authors.ExtraControlAttributes);
        Assert.Contains("[label]=\"t('Authors')\"", authors.ExtraControlAttributes);
    }

    [Fact]
    public void Build_CalendarDateTime_AddsShowTimeConfigFlagDefaultFalse()
    {
        FieldModel publishedAt = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "PublishedAt");

        Assert.Equal("spiderly-calendar", publishedAt.ControlTag);
        Assert.Equal("publishedAt", publishedAt.FormControlName);
        Assert.Contains("showPublishedAtTime", publishedAt.ExtraConfigFlags);
        Assert.Equal(" [showTime]=\"config.showPublishedAtTime === true\"", publishedAt.ExtraControlAttributes);
    }

    [Fact]
    public void Build_CalendarDateOnly_UsesStaticLiteralNoExtraFlag()
    {
        FieldModel birthDate = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "BirthDate");

        Assert.Equal("spiderly-calendar", birthDate.ControlTag);
        Assert.Equal(" [dateOnly]=\"true\"", birthDate.ExtraControlAttributes);
        Assert.Empty(birthDate.ExtraConfigFlags);
    }

    [Fact]
    public void Build_CalendarTimeOnly_UsesStaticLiteralNoExtraFlag()
    {
        FieldModel openTime = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "OpenTime");

        Assert.Equal("spiderly-calendar", openTime.ControlTag);
        Assert.Equal(" [timeOnly]=\"true\"", openTime.ExtraControlAttributes);
        Assert.Empty(openTime.ExtraConfigFlags);
    }

    [Fact]
    public void Build_ColorPicker_AddsShowTextFieldConfigFlagDefaultTrue()
    {
        FieldModel color = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Color");

        Assert.Equal("spiderly-colorpicker", color.ControlTag);
        Assert.Equal("color", color.FormControlName);
        Assert.Contains("showColorTextField", color.ExtraConfigFlags);
        Assert.Equal(" [showInputTextField]=\"config.showColorTextField !== false\"", color.ExtraControlAttributes);
    }

    [Fact]
    public void Build_Editor_PlainHasNoUploadOrExtraAttributes()
    {
        FieldModel content = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Content");

        Assert.Equal("spiderly-editor", content.ControlTag);
        Assert.Equal("content", content.FormControlName);
        Assert.Equal("", content.ExtraControlAttributes);
        Assert.Null(content.EditorImageUpload);
    }

    [Fact]
    public void Build_EditorS3_HasUploadImageMethodAndObjectId()
    {
        FieldModel bio = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Bio");

        Assert.Equal("spiderly-editor", bio.ControlTag);
        Assert.NotNull(bio.EditorImageUpload);
        Assert.Equal("uploadBioImage", bio.EditorImageUpload.MethodName);
        Assert.Equal("uploadBioImageForBrand", bio.EditorImageUpload.ApiMethodName);
        Assert.Contains("[uploadImageMethod]=\"uploadBioImage\"", bio.ExtraControlAttributes);
        Assert.Contains("[objectId]=\"formGroup.controls.brandDTO.controls.id.getRawValue()\"", bio.ExtraControlAttributes);
    }

    [Fact]
    public void Build_File_HasUploadMethodOutputAndAttributeBundle()
    {
        FieldModel photo = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Photo");

        Assert.Equal("spiderly-file", photo.ControlTag);
        Assert.Equal("photo", photo.FormControlName);
        Assert.NotNull(photo.FileUpload);
        Assert.Equal("uploadPhoto", photo.FileUpload.MethodName);
        Assert.Equal("uploadPhotoForBrand", photo.FileUpload.ApiMethodName);
        Assert.Equal("onPhotoUploaded", photo.FileUpload.OutputName);

        Assert.Contains("[fileData]=\"formGroup.controls.brandDTO.controls.photoData.getRawValue()\"", photo.ExtraControlAttributes);
        Assert.Contains("[objectId]=\"formGroup.controls.brandDTO.controls.id.getRawValue()\"", photo.ExtraControlAttributes);
        Assert.Contains("(onFileSelected)=\"uploadPhoto($event, formGroup.controls.brandDTO)\"", photo.ExtraControlAttributes);
        Assert.Contains("[disabled]=\"!isAuthorizedForSave\"", photo.ExtraControlAttributes);
        Assert.Contains("[isUrlFileData]=\"false\"", photo.ExtraControlAttributes);
    }

    [Fact]
    public void Build_File_OmitsOptionalAttributesWhenAbsent()
    {
        FieldModel photo = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Photo");

        Assert.DoesNotContain("[imageWidth]", photo.ExtraControlAttributes);
        Assert.DoesNotContain("[acceptedFileTypes]", photo.ExtraControlAttributes);
        Assert.DoesNotContain("[maxFileSize]", photo.ExtraControlAttributes);
    }

    [Fact]
    public void Build_BackReferenceM2O_SetsParentRelationNameFromWithMany()
    {
        FieldModel parentGroup = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "ParentGroup");

        Assert.Equal("spiderly-autocomplete", parentGroup.ControlTag);
        Assert.Equal("parentGroupId", parentGroup.FormControlName);
        Assert.Equal("Brands", parentGroup.ParentRelationName);
    }

    [Fact]
    public void Build_PlainM2O_HasNullParentRelationName()
    {
        FieldModel country = NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Fields.Single(f => f.PropertyName == "Country");

        Assert.Null(country.ParentRelationName);
    }

    [Fact]
    public void Build_OrderedOneToMany_PopulatesCompositionModel()
    {
        OrderedOneToManyModel block = NgFieldsModelBuilder.Build(Segmentation(), new() { Segmentation() }, new()).OrderedOneToManies.Single();

        Assert.Equal("SegmentationItems", block.PropertyName);
        Assert.Equal("SegmentationItems", block.TranslationKey);
        Assert.Equal("formGroup.controls.orderedSegmentationItemsSaveBodyDTO", block.FormArrayAccess);
        Assert.Equal("segmentationItemFormGroup", block.ChildRowVar);
        Assert.Equal("segmentation-item-fields", block.ChildFieldsSelector);
        Assert.Equal("SegmentationItemFieldsComponent", block.ChildFieldsComponentClassName);
        Assert.Equal("AddNewSegmentationItem", block.AddNewLabelKey);
        Assert.Equal("segmentationItemsPanelCollapsed", block.PanelCollapsedInputName);
        Assert.Equal("additionalContentTemplateForSegmentationItems", block.AdditionalContentTemplateInputName);
    }

    [Fact]
    public void Build_OrderedOneToMany_IsNotAddedToScalarFields()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(Segmentation(), new() { Segmentation() }, new());

        Assert.DoesNotContain(model.Fields, f => f.PropertyName == "SegmentationItems");
        Assert.Contains(model.Fields, f => f.PropertyName == "Name");
    }

    [Fact]
    public void Build_NoOrderedOneToMany_LeavesEmptyList()
    {
        Assert.Empty(NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).OrderedOneToManies);
    }

    [Fact]
    public void Build_ComplexReadonlyTable_PopulatesTableModel()
    {
        TableModel table = NgFieldsModelBuilder.Build(User(), new() { User(), Role() }, new()).Tables.Single();

        Assert.Equal("Roles", table.TranslationKey);
        Assert.Equal("rolesTableCols", table.ColsFieldName);
        Assert.Equal("Role", table.ColsTypeArgument);
        Assert.Equal("getPaginatedRolesListObservableMethod", table.PaginatedListFieldName);
        Assert.Equal("this.apiService.getPaginatedRolesListForUser", table.PaginatedListApiCall);
        Assert.Equal("exportRolesListToExcelObservableMethod", table.ExportFieldName);
        Assert.Equal("this.apiService.exportRolesListToExcelForUser", table.ExportApiCall);
        Assert.True(table.IsReadonly);
        Assert.Single(table.ColumnDefs);
        Assert.Contains("this.translocoService.translate('Name')", table.ColumnDefs[0]);
        Assert.Contains("filterType: 'text'", table.ColumnDefs[0]);
        Assert.Contains("field: 'name'", table.ColumnDefs[0]);
    }

    [Fact]
    public void Build_Table_IsNotAddedToScalarFields()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(User(), new() { User(), Role() }, new());
        Assert.DoesNotContain(model.Fields, f => f.PropertyName == "Roles");
        Assert.Contains(model.Fields, f => f.PropertyName == "Email");
    }

    [Fact]
    public void Build_NoTable_LeavesEmptyList()
    {
        Assert.Empty(NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).Tables);
    }

    [Fact]
    public void Build_SimpleLazyLoadTable_PopulatesEditableTableModel()
    {
        TableModel table = NgFieldsModelBuilder.Build(UserWithEditableTable(), new() { UserWithEditableTable(), Permission() }, new()).Tables.Single();

        Assert.False(table.IsReadonly);
        Assert.Equal("permissionsTableCols", table.ColsFieldName);
        Assert.Equal("Permission", table.ColsTypeArgument);
        Assert.Equal("newlySelectedPermissionsIds", table.NewlySelectedField);
        Assert.Equal("unselectedPermissionsIds", table.UnselectedField);
        Assert.Equal("areAllPermissionsSelected", table.AreAllSelectedField);
        Assert.Equal("lastPermissionsLazyLoadTableFilter", table.LastFilterField);
        Assert.Equal("selectedPermissionsIds", table.SelectedFormControl);
        Assert.Equal("unselectedPermissionsIds", table.UnselectedFormControl);
        Assert.Equal("areAllPermissionsSelected", table.AreAllSelectedFormControl);
        Assert.Equal("permissionsTableFilter", table.TableFilterFormControl);
        Assert.Equal("selectedPermissionsLazyLoadMethod", table.LazyLoadMethodName);
        Assert.Equal("this.apiService.lazyLoadSelectedPermissionsIdsForUser", table.LazyLoadApiCall);
        Assert.Equal("areAllPermissionsSelectedChange", table.AreAllSelectedChangeMethodName);
        Assert.Equal("onPermissionsLazyLoad", table.OnLazyLoadMethodName);
        Assert.Equal("this.formGroup.controls.userDTO.controls.id.getRawValue()", table.ParentIdRawValueExpression);
    }

    [Fact]
    public void Build_ComplexReadonlyTable_HasNoEditableFacts()
    {
        TableModel table = NgFieldsModelBuilder.Build(User(), new() { User(), Role() }, new()).Tables.Single();
        Assert.True(table.IsReadonly);
        Assert.Null(table.NewlySelectedField);
        Assert.Null(table.LazyLoadMethodName);
    }

    [Fact]
    public void Build_AssignsSectionNameToFields()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(SectionedEntity(), new() { SectionedEntity() }, new());

        Assert.Equal("General", model.Fields.Single(f => f.PropertyName == "Name").SectionName);
        Assert.Null(model.Fields.Single(f => f.PropertyName == "Note").SectionName);
        Assert.Equal("Security", model.Fields.Single(f => f.PropertyName == "Secret").SectionName);
    }

    [Fact]
    public void Build_SectionOrder_IsFirstAppearanceIncludingImplicitHeaderless()
    {
        List<string> order = NgFieldsModelBuilder.Build(SectionedEntity(), new() { SectionedEntity() }, new()).SectionOrder;

        Assert.Equal(new string[] { "General", null, "Security" }, order);
    }

    [Fact]
    public void Build_NoSection_LeavesSectionOrderEmpty()
    {
        Assert.Empty(NgFieldsModelBuilder.Build(Brand(), new() { Brand() }, new()).SectionOrder);
    }

    [Fact]
    public void Build_ComplexManyToManyList_SetsPanelAndFormArray()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(
            ProductVariantWithWarehouses(),
            new() { ProductVariantWithWarehouses(), ProductVariantWarehouse(), Warehouse() },
            new());

        ComplexManyToManyListModel c = Assert.Single(model.ComplexManyToManyLists);
        Assert.Equal("ProductVariantWarehouses", c.PropertyName);
        Assert.Equal("ProductVariantWarehouses", c.TranslationKey);
        Assert.Equal("formGroup.controls.productVariantWarehouses", c.FormArrayAccess);
        Assert.Equal("productVariantWarehouseFormGroup", c.JunctionRowVar);
        Assert.Equal(
            "productVariantWarehouseFormGroup.getControl('warehouseDisplayName')?.getRawValue()",
            c.HeaderExpression);
        Assert.Equal("productVariantWarehousesPanelCollapsed", c.PanelCollapsedInputName);
        Assert.Null(c.SectionName);
    }

    [Fact]
    public void Build_ComplexManyToManyList_RendersOnlyPayloadFieldsInline()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(
            ProductVariantWithWarehouses(),
            new() { ProductVariantWithWarehouses(), ProductVariantWarehouse(), Warehouse() },
            new());

        ComplexManyToManyListModel c = Assert.Single(model.ComplexManyToManyLists);

        // The two [M2MWithMany] relation props are excluded (IsManyToOneType); only the Stock payload field renders.
        ComplexM2MJunctionFieldModel field = Assert.Single(c.JunctionFields);
        Assert.Equal("spiderly-number", field.ControlTag);
        Assert.Equal("stock", field.FormControlName);
        Assert.Equal("", field.ExtraControlAttributes);
    }

    [Fact]
    public void Build_ComplexManyToManyList_NotTreatedAsFieldOrTable()
    {
        FieldsComponentModel model = NgFieldsModelBuilder.Build(
            ProductVariantWithWarehouses(),
            new() { ProductVariantWithWarehouses(), ProductVariantWarehouse(), Warehouse() },
            new());

        Assert.DoesNotContain(model.Fields, f => f.PropertyName == "ProductVariantWarehouses");
        Assert.Empty(model.Tables);
    }
}
