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

                model.OrderedOneToManies.Add(new OrderedOneToManyModel
                {
                    PropertyName = property.Name,
                    TranslationKey = property.Name,
                    FormArrayAccess = $"formGroup.controls.ordered{property.Name}SaveBodyDTO",
                    ChildRowVar = $"{childCamel}FormGroup",
                    ChildFieldsSelector = $"{childType.FromPascalToKebabCase()}-fields",
                    ChildFieldsComponentClassName = $"{childType}FieldsComponent",
                    AddNewLabelKey = $"AddNew{childType}",
                    PanelCollapsedInputName = $"{property.Name.FirstCharToLower()}PanelCollapsed",
                    AdditionalContentTemplateInputName = $"additionalContentTemplateFor{property.Name}",
                });
            }

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
