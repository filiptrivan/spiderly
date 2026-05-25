using System.Linq;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Builds a <see cref="FieldsComponentModel"/> from a <see cref="SpiderlyClass"/>, reusing the existing
    /// control-type / form-control-name / width source-of-truth helpers in
    /// <see cref="NgDetailsPropertyBlockGenerator"/>. Currently handles scalar controls (TextBox, Integer,
    /// Decimal, CheckBox), M2O Autocomplete, Dropdown (enum/explicit), TextArea, Password, and TextBlock;
    /// <see cref="BuildField"/> returns null for control types not yet handled (Calendar, MultiSelect, etc.).
    /// </summary>
    internal static class NgFieldsModelBuilder
    {
        internal static FieldsComponentModel Build(SpiderlyClass entity)
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
                default:
                    return null; // control types beyond current slices are added in later slices
            }
        }
    }
}
