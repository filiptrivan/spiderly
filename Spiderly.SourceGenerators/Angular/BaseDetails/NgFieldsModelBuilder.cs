using System.Collections.Generic;
using System.Linq;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Builds a <see cref="FieldsComponentModel"/> from a <see cref="SpiderlyClass"/>, reusing the existing
    /// control-type / form-control-name / width source-of-truth helpers in
    /// <see cref="NgDetailsPropertyBlockGenerator"/>. <see cref="BuildField"/> maps each supported control type
    /// (see the switch) to a <see cref="FieldModel"/> and returns null for control types not yet handled.
    /// </summary>
    internal static class NgFieldsModelBuilder
    {
        // allEntities and customDTOClasses are reserved for M2M-table column filter type resolution
        // (consumed in a later task) and are intentionally unused by the current field/ordered-O2M logic.
        internal static FieldsComponentModel Build(SpiderlyClass entity, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses)
        {
            FieldsComponentModel model = new()
            {
                EntityName = entity.Name,
                Selector = $"{entity.Name.FromPascalToKebabCase()}-fields",
                ComponentClassName = $"{entity.Name}FieldsComponent",
                SaveBodyTypeName = $"{entity.Name}SaveBody",
                ConfigClassName = $"{entity.Name}FieldsConfig",
                MainDtoAccess = $"formGroup.controls.{entity.Name.FirstCharToLower()}DTO",
            };

            foreach (SpiderlyProperty property in NgDetailsPropertyBlockGenerator.GetOrderedPropertiesForUIBlocks(entity.Properties.ToList(), entity))
            {
                FieldModel field = BuildField(property, model.MainDtoAccess, entity.Name);
                if (field != null)
                    model.Fields.Add(field);
            }

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                string childType = Helpers.ExtractTypeFromGenericType(property.Type);
                string childCamel = childType.FirstCharToLower();
                string childRowVar = $"{childCamel}FormGroup";

                SpiderlyClass orderedChildEntity = Helpers.GetEntityByPropertyType(property, allEntities);
                List<OrderedChildFileOutputModel> fileOutputs = new();
                if (orderedChildEntity != null)
                {
                    foreach (SpiderlyProperty childProp in NgDetailsPropertyBlockGenerator.GetDetailsUiFileProperties(orderedChildEntity))
                    {
                        fileOutputs.Add(new OrderedChildFileOutputModel
                        {
                            ParentOutputName = $"on{childProp.Name}For{childType}Uploaded",
                            ChildUploadOutputName = $"on{childProp.Name}Uploaded",
                            RowDtoAccess = $"{childRowVar}.controls.{childCamel}DTO",
                        });
                    }
                }

                model.OrderedOneToManies.Add(new OrderedOneToManyModel
                {
                    PropertyName = property.Name,
                    TranslationKey = property.Name,
                    FormArrayAccess = $"formGroup.controls.ordered{property.Name}SaveBodyDTO",
                    ChildRowVar = childRowVar,
                    ChildFieldsSelector = $"{childType.FromPascalToKebabCase()}-fields",
                    ChildFieldsComponentClassName = $"{childType}FieldsComponent",
                    AddNewLabelKey = $"AddNew{childType}",
                    PanelCollapsedInputName = $"{property.Name.FirstCharToLower()}PanelCollapsed",
                    AdditionalContentTemplateInputName = $"additionalContentTemplateFor{property.Name}",
                    SectionName = NgDetailsPropertyBlockGenerator.GetUISectionName(property),
                    FileOutputs = fileOutputs,
                });
            }

            foreach (SpiderlyProperty property in entity.Properties
                .Where(p => p.IsIncludedInDetailsUi(entity) && p.HasComplexManyToManyReadonlyTableAttribute()))
            {
                string childType = Helpers.ExtractTypeFromGenericType(property.Type);
                string propCamel = property.Name.FirstCharToLower();

                model.Tables.Add(new TableModel
                {
                    TranslationKey = property.Name,
                    ColsFieldName = $"{propCamel}TableCols",
                    ColsTypeArgument = childType,
                    ColumnDefs = NgDetailsDataGenerator.GetSimpleManyToManyTableLazyLoadCols(property, entity, allEntities, customDTOClasses),
                    PaginatedListFieldName = $"getPaginated{property.Name}ListObservableMethod",
                    PaginatedListApiCall = $"this.apiService.getPaginated{property.Name}ListFor{entity.Name}",
                    ExportFieldName = $"export{property.Name}ListToExcelObservableMethod",
                    ExportApiCall = $"this.apiService.export{property.Name}ListToExcelFor{entity.Name}",
                    IsReadonly = true,
                    SectionName = NgDetailsPropertyBlockGenerator.GetUISectionName(property),
                });
            }

            foreach (SpiderlyProperty property in entity.Properties
                .Where(p => p.IsIncludedInDetailsUi(entity) && p.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                string childType = Helpers.ExtractTypeFromGenericType(property.Type);
                string propCamel = property.Name.FirstCharToLower();

                model.Tables.Add(new TableModel
                {
                    TranslationKey = property.Name,
                    ColsFieldName = $"{propCamel}TableCols",
                    ColsTypeArgument = childType,
                    ColumnDefs = NgDetailsDataGenerator.GetSimpleManyToManyTableLazyLoadCols(property, entity, allEntities, customDTOClasses),
                    PaginatedListFieldName = $"getPaginated{property.Name}ListObservableMethod",
                    PaginatedListApiCall = $"this.apiService.getPaginated{property.Name}ListFor{entity.Name}",
                    ExportFieldName = $"export{property.Name}ListToExcelObservableMethod",
                    ExportApiCall = $"this.apiService.export{property.Name}ListToExcelFor{entity.Name}",
                    IsReadonly = false,
                    NewlySelectedField = $"newlySelected{property.Name}Ids",
                    UnselectedField = $"unselected{property.Name}Ids",
                    AreAllSelectedField = $"areAll{property.Name}Selected",
                    LastFilterField = $"last{property.Name}LazyLoadTableFilter",
                    SelectedFormControl = $"selected{property.Name}Ids",
                    UnselectedFormControl = $"unselected{property.Name}Ids",
                    AreAllSelectedFormControl = $"areAll{property.Name}Selected",
                    TableFilterFormControl = $"{propCamel}TableFilter",
                    LazyLoadMethodName = $"selected{property.Name}LazyLoadMethod",
                    LazyLoadApiCall = $"this.apiService.lazyLoadSelected{property.Name}IdsFor{entity.Name}",
                    AreAllSelectedChangeMethodName = $"areAll{property.Name}SelectedChange",
                    OnLazyLoadMethodName = $"on{property.Name}LazyLoad",
                    ParentIdRawValueExpression = $"this.{model.MainDtoAccess}.controls.id.getRawValue()",
                    SectionName = NgDetailsPropertyBlockGenerator.GetUISectionName(property),
                });
            }

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                var (junctionEntity, _, otherSideM2MProperty, _) =
                    NgDetailsPropertyBlockGenerator.ResolveComplexManyToManyListInfo(entity, property, allEntities);

                string junctionCamel = junctionEntity.Name.FirstCharToLower();
                string junctionRowVar = $"{junctionCamel}FormGroup";

                List<ComplexM2MJunctionFieldModel> junctionFields = junctionEntity.Properties
                    .Where(p => !p.IsManyToOneType() && !p.Type.IsOneToManyType() && !p.HasUIDoNotGenerateAttribute())
                    .Select(p =>
                    {
                        UIControlTypeCodes ct = NgDetailsPropertyBlockGenerator.GetUIControlType(p);
                        string extraDecimalAttrs = ct == UIControlTypeCodes.Decimal
                            ? $" [decimal]=\"true\" [maxFractionDigits]=\"{p.GetDecimalScale()}\""
                            : "";
                        return new ComplexM2MJunctionFieldModel
                        {
                            ControlTag = NgDetailsPropertyBlockGenerator.GetUIStringControlType(ct),
                            FormControlName = p.Name.FirstCharToLower(),
                            ExtraControlAttributes = extraDecimalAttrs,
                        };
                    })
                    .ToList();

                model.ComplexManyToManyLists.Add(new ComplexManyToManyListModel
                {
                    PropertyName = property.Name,
                    TranslationKey = property.Name,
                    FormArrayAccess = $"formGroup.controls.{property.Name.FirstCharToLower()}",
                    JunctionRowVar = junctionRowVar,
                    HeaderExpression = $"{junctionRowVar}.getControl('{otherSideM2MProperty.Name.FirstCharToLower()}DisplayName')?.getRawValue()",
                    PanelCollapsedInputName = $"{property.Name.FirstCharToLower()}PanelCollapsed",
                    JunctionFields = junctionFields,
                    SectionName = NgDetailsPropertyBlockGenerator.GetUISectionName(property),
                });
            }

            // Distinct sections in first-appearance order over the SAME property set the fragment actually renders
            // (entity.Properties — custom-DTO display fields aren't handled by the fragment yet). Only enable grouped
            // mode (a non-empty SectionOrder) when at least one named section exists, so fully-unsectioned entities
            // keep the flat path (byte-identical output).
            List<string> sectionOrder = new();
            foreach (SpiderlyProperty property in NgDetailsPropertyBlockGenerator.GetOrderedPropertiesForUIBlocks(entity.Properties.ToList(), entity))
            {
                string section = NgDetailsPropertyBlockGenerator.GetUISectionName(property);
                if (!sectionOrder.Contains(section))
                    sectionOrder.Add(section);
            }
            if (sectionOrder.Any(s => s != null))
                model.SectionOrder = sectionOrder;

            return model;
        }

        private static FieldModel BuildField(SpiderlyProperty property, string mainDtoAccess, string entityName)
        {
            UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(property);

            FieldModel field = new()
            {
                PropertyName = property.Name,
                FormControlName = NgDetailsPropertyBlockGenerator.GetFormControlName(property),
                ConfigShowFlagName = $"show{property.Name}",
                Width = NgDetailsPropertyBlockGenerator.GetUIControlWidth(property, isFromOrderedOneToMany: false),
                ParentRelationName = property.WithMany(),
                SectionName = NgDetailsPropertyBlockGenerator.GetUISectionName(property),
            };

            switch (controlType)
            {
                case UIControlTypeCodes.TextBox:
                    field.ControlTag = "spiderly-textbox";
                    return field;
                case UIControlTypeCodes.Integer:
                    field.ControlTag = "spiderly-number";
                    return field;
                case UIControlTypeCodes.Decimal:
                    field.ControlTag = "spiderly-number";
                    field.ExtraControlAttributes = $" [decimal]=\"true\" [maxFractionDigits]=\"{property.GetDecimalScale()}\"";
                    return field;
                case UIControlTypeCodes.CheckBox:
                    field.ControlTag = "spiderly-checkbox";
                    field.ChangeOutput = new FieldOutputModel
                    {
                        ControlEventName = "onChange",
                        OutputName = $"on{property.Name}Change",
                        EventType = "CheckboxChangeEvent",
                    };
                    return field;
                case UIControlTypeCodes.Autocomplete:
                    field.ControlTag = "spiderly-autocomplete";
                    field.OptionsFieldName = $"{property.Name.FirstCharToLower()}Options";
                    field.Search = new FieldSearchModel
                    {
                        MethodName = $"search{property.Name}",
                        ApiMethodName = $"get{property.Name}AutocompleteListFor{entityName}",
                        OptionsFieldName = field.OptionsFieldName,
                    };
                    field.ExtraControlAttributes =
                        $" [options]=\"{field.OptionsFieldName}\""
                        + $" [displayName]=\"{mainDtoAccess}.controls.{property.Name.FirstCharToLower()}DisplayName.getRawValue()\""
                        + $" (onTextInput)=\"{field.Search.MethodName}($event, {mainDtoAccess}.controls.id.getRawValue())\"";
                    return field;
                case UIControlTypeCodes.TextArea:
                    field.ControlTag = "spiderly-textarea";
                    return field;
                case UIControlTypeCodes.Password:
                    field.ControlTag = "spiderly-password";
                    return field;
                case UIControlTypeCodes.TextBlock:
                    field.ControlTag = "spiderly-textblock";
                    return field;
                case UIControlTypeCodes.Dropdown:
                    field.ControlTag = "spiderly-dropdown";
                    field.OptionsFieldName = $"{property.Name.FirstCharToLower()}Options";
                    field.OptionsIsInput = true;
                    field.ChangeOutput = new FieldOutputModel
                    {
                        ControlEventName = "onChange",
                        OutputName = $"on{property.Name}Change",
                        EventType = "DropdownChangeEvent",
                    };
                    field.ExtraControlAttributes = $" [options]=\"{field.OptionsFieldName}\"";
                    return field;
                case UIControlTypeCodes.MultiSelect:
                    field.ControlTag = "spiderly-multiselect";
                    field.BindsOnSaveBody = true;
                    field.OptionsFieldName = $"{property.Name.FirstCharToLower()}Options";
                    field.OptionsIsInput = true;
                    field.ExtraControlAttributes = $" [options]=\"{field.OptionsFieldName}\" [label]=\"t('{property.Name}')\"";
                    return field;
                case UIControlTypeCodes.MultiAutocomplete:
                    field.ControlTag = "spiderly-multiautocomplete";
                    field.BindsOnSaveBody = true;
                    field.OptionsFieldName = $"{property.Name.FirstCharToLower()}Options";
                    field.Search = new FieldSearchModel
                    {
                        MethodName = $"search{property.Name}",
                        ApiMethodName = $"get{property.Name}AutocompleteListFor{entityName}",
                        OptionsFieldName = field.OptionsFieldName,
                    };
                    field.ExtraControlAttributes =
                        $" [options]=\"{field.OptionsFieldName}\""
                        + $" (onTextInput)=\"{field.Search.MethodName}($event, {mainDtoAccess}.controls.id.getRawValue())\""
                        + $" [label]=\"t('{property.Name}')\"";
                    return field;
                case UIControlTypeCodes.Calendar:
                    field.ControlTag = "spiderly-calendar";
                    if (property.Type.IsDateOnly())
                    {
                        field.ExtraControlAttributes = " [dateOnly]=\"true\"";
                    }
                    else if (property.Type.IsTimeOnly())
                    {
                        field.ExtraControlAttributes = " [timeOnly]=\"true\"";
                    }
                    else
                    {
                        string showTimeFlag = $"show{property.Name}Time";
                        field.ExtraConfigFlags.Add(showTimeFlag);
                        field.ExtraControlAttributes = $" [showTime]=\"config.{showTimeFlag} === true\"";
                    }
                    return field;
                case UIControlTypeCodes.ColorPicker:
                {
                    field.ControlTag = "spiderly-colorpicker";
                    string showTextFieldFlag = $"show{property.Name}TextField";
                    field.ExtraConfigFlags.Add(showTextFieldFlag);
                    field.ExtraControlAttributes = $" [showInputTextField]=\"config.{showTextFieldFlag} !== false\"";
                    return field;
                }
                case UIControlTypeCodes.Editor:
                    field.ControlTag = "spiderly-editor";
                    if (property.HasS3PublicStorageAttribute())
                    {
                        field.EditorImageUpload = new EditorImageUploadModel
                        {
                            MethodName = $"upload{property.Name}Image",
                            ApiMethodName = $"upload{property.Name}ImageFor{entityName}",
                        };
                        field.ExtraControlAttributes =
                            $" [uploadImageMethod]=\"{field.EditorImageUpload.MethodName}\""
                            + $" [objectId]=\"{mainDtoAccess}.controls.id.getRawValue()\"";
                    }
                    return field;
                case UIControlTypeCodes.File:
                {
                    field.ControlTag = "spiderly-file";
                    field.FileUpload = new FileUploadModel
                    {
                        MethodName = $"upload{property.Name}",
                        ApiMethodName = $"upload{property.Name}For{entityName}",
                        OutputName = $"on{property.Name}Uploaded",
                    };

                    string propCamel = property.Name.FirstCharToLower();
                    List<string> fileAttrs = new()
                    {
                        $"[fileData]=\"{mainDtoAccess}.controls.{propCamel}Data.getRawValue()\"",
                        $"[objectId]=\"{mainDtoAccess}.controls.id.getRawValue()\"",
                        $"(onFileSelected)=\"{field.FileUpload.MethodName}($event, {mainDtoAccess})\"",
                        "[disabled]=\"!isAuthorizedForSave\"",
                        $"[isUrlFileData]=\"{property.IsPublicUrl().ToString().ToLower()}\"",
                    };

                    string dims = NgDetailsPropertyBlockGenerator.GetImageDimensionsHtmlAttributes(property);
                    if (dims.Length > 0) fileAttrs.Add(dims);
                    string acceptedTypes = NgDetailsPropertyBlockGenerator.GetAcceptedFileTypesHtmlAttribute(property);
                    if (acceptedTypes.Length > 0) fileAttrs.Add(acceptedTypes);
                    string maxSize = NgDetailsPropertyBlockGenerator.GetMaxFileSizeHtmlAttribute(property);
                    if (maxSize.Length > 0) fileAttrs.Add(maxSize);

                    field.ExtraControlAttributes = " " + string.Join(" ", fileAttrs);
                    return field;
                }
                default:
                    return null; // control types beyond current slices are added in later slices
            }
        }
    }
}
