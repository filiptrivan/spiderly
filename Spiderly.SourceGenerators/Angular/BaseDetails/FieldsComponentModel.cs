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
    }

    /// <summary>A control change event wired to a component <c>@Output()</c>.</summary>
    internal sealed class FieldOutputModel
    {
        public string ControlEventName { get; set; }
        public string OutputName { get; set; }
        public string EventType { get; set; }
    }
}
