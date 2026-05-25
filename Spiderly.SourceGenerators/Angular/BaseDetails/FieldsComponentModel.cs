using System.Collections.Generic;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Structured emission model for one entity's redesigned "fields fragment" ({Entity}Fields). Every per-field
    /// fact is computed once here so the fragment emitter and the config-class emitter read a single source of
    /// truth — their cross-referencing names (config.showX, the showX? config field, the form-control binding)
    /// cannot drift.
    /// </summary>
    internal sealed class FieldsComponentModel
    {
        public string EntityName { get; set; }
        public string Selector { get; set; }
        public string ComponentClassName { get; set; }
        public string SaveBodyTypeName { get; set; }
        public string ConfigClassName { get; set; }

        /// <summary>
        /// Form-group access expression for the entity's editable fields, e.g. <c>formGroup.controls.brandDTO</c>.
        /// The fragment's <c>formGroup</c> input is the {Entity}SaveBody group; its scalar/M2O/dropdown/autocomplete
        /// controls live under the nested {entityCamel}DTO control. (Collection controls like multiselect bind
        /// directly on <c>formGroup</c> and are handled in a later slice.)
        /// </summary>
        public string MainDtoAccess { get; set; }

        public List<FieldModel> Fields { get; set; } = new();
    }

    /// <summary>Per-property facts the fragment template and config class are built from.</summary>
    internal sealed class FieldModel
    {
        public string PropertyName { get; set; }
        public string ControlTag { get; set; }
        public string FormControlName { get; set; }
        public string ConfigShowFlagName { get; set; }
        public string Width { get; set; }

        /// <summary>Extra control attributes (with a leading space), e.g. <c> [decimal]="true" [maxFractionDigits]="2"</c>. Empty when none.</summary>
        public string ExtraControlAttributes { get; set; } = "";

        /// <summary>Set for controls that emit a change event (e.g. CheckBox); null otherwise.</summary>
        public FieldOutputModel ChangeOutput { get; set; }

        /// <summary>Self-owned options array field name for option-backed controls (e.g. "countryOptions"); null otherwise.</summary>
        public string OptionsFieldName { get; set; }

        /// <summary>
        /// True when the options array is supplied by the parent/shell as an <c>@Input</c> (e.g. Dropdown — static
        /// options loaded once and passed down). False when the fragment owns it (e.g. Autocomplete fills it on demand).
        /// </summary>
        public bool OptionsIsInput { get; set; }

        /// <summary>On-demand autocomplete search method backing a M2O autocomplete control; null otherwise.</summary>
        public FieldSearchModel Search { get; set; }
    }

    /// <summary>A control change event wired to a component <c>@Output()</c>.</summary>
    internal sealed class FieldOutputModel
    {
        public string ControlEventName { get; set; }
        public string OutputName { get; set; }
        public string EventType { get; set; }
    }

    /// <summary>An on-demand autocomplete search method backing a M2O autocomplete control.</summary>
    internal sealed class FieldSearchModel
    {
        public string MethodName { get; set; }
        public string ApiMethodName { get; set; }
        public string OptionsFieldName { get; set; }
    }
}
