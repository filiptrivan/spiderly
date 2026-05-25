using System.Linq;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Builds a <see cref="FieldsComponentModel"/> from a <see cref="SpiderlyClass"/>, reusing the existing
    /// control-type / form-control-name / width source-of-truth helpers in
    /// <see cref="NgDetailsPropertyBlockGenerator"/>. Slice 2 supports the data-free scalar controls
    /// (TextBox, Number, CheckBox); <see cref="BuildField"/> returns null for control types added in later slices.
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
            };

            foreach (SpiderlyProperty property in NgDetailsPropertyBlockGenerator.GetOrderedPropertiesForUIBlocks(entity.Properties.ToList(), entity))
            {
                FieldModel field = BuildField(property);
                if (field != null)
                    model.Fields.Add(field);
            }

            return model;
        }

        private static FieldModel BuildField(SpiderlyProperty property)
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
                default:
                    return null; // control types beyond Slice 2 are added in later slices
            }
        }
    }
}
