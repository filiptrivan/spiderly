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
        /// controls live under the nested {entityCamel}DTO control. Collection controls (MultiSelect/MultiAutocomplete)
        /// bind directly on <c>formGroup</c> via <see cref="FieldModel.BindsOnSaveBody"/>.
        /// </summary>
        public string MainDtoAccess { get; set; }

        public List<FieldModel> Fields { get; set; } = new();

        /// <summary>
        /// Ordered-one-to-many child collections this entity owns ([UIOrderedOneToMany]). Each renders as a panel of
        /// index-cards that COMPOSE the child's own fragment (passing hiddenParentRelation to hide the back-ref),
        /// rather than flattening the child's controls.
        /// </summary>
        public List<OrderedOneToManyModel> OrderedOneToManies { get; set; } = new();
    }

    /// <summary>An [UIOrderedOneToMany] child collection rendered as a composed panel of index-cards.</summary>
    internal sealed class OrderedOneToManyModel
    {
        public string PropertyName { get; set; }
        public string TranslationKey { get; set; }
        public string FormArrayAccess { get; set; }
        public string ChildRowVar { get; set; }
        public string ChildFieldsSelector { get; set; }
        public string ChildFieldsComponentClassName { get; set; }
        public string AddNewLabelKey { get; set; }
        public string PanelCollapsedInputName { get; set; }
        public string AdditionalContentTemplateInputName { get; set; }
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

        /// <summary>
        /// Extra config-class flags this field contributes beyond <see cref="ConfigShowFlagName"/> (e.g. a Calendar's
        /// show-time toggle or a ColorPicker's show-text-field toggle). Each entry becomes an optional boolean on the
        /// {Entity}FieldsConfig class; the binding that consumes it — carrying its own default — is baked into
        /// <see cref="ExtraControlAttributes"/>.
        /// </summary>
        public List<string> ExtraConfigFlags { get; set; } = new();

        /// <summary>Set for controls that emit a change event (e.g. CheckBox); null otherwise.</summary>
        public FieldOutputModel ChangeOutput { get; set; }

        /// <summary>Self-owned options array field name for option-backed controls (e.g. "countryOptions"); null otherwise.</summary>
        public string OptionsFieldName { get; set; }

        /// <summary>
        /// True when the options array is supplied by the parent/shell as an <c>@Input</c> (static options loaded
        /// once and passed down, e.g. Dropdown/MultiSelect). False when the fragment owns it and fills it on demand
        /// (e.g. Autocomplete/MultiAutocomplete via the search method).
        /// </summary>
        public bool OptionsIsInput { get; set; }

        /// <summary>
        /// True for collection controls (MultiSelect/MultiAutocomplete) whose form control lives at the SaveBody
        /// level (e.g. <c>selected{Prop}Ids</c>) — the emitter binds them on <c>formGroup</c> directly rather than
        /// through the nested <c>{entityCamel}DTO</c> control.
        /// </summary>
        public bool BindsOnSaveBody { get; set; }

        /// <summary>On-demand autocomplete search method backing a M2O autocomplete control; null otherwise.</summary>
        public FieldSearchModel Search { get; set; }

        /// <summary>
        /// Editor image-upload method backing an <c>[S3PublicStorage]</c> Editor control; null otherwise. The
        /// fragment declares an arrow-fn <c>{MethodName}</c> that delegates to <c>ApiService.{ApiMethodName}</c>.
        /// </summary>
        public EditorImageUploadModel EditorImageUpload { get; set; }

        /// <summary>
        /// File blob-upload method backing a File control; null otherwise. The fragment declares a void
        /// <c>{MethodName}</c> that uploads via <c>ApiService.{ApiMethodName}</c>, writes the returned filename into
        /// the field's form control, and emits <c>{OutputName}</c>. A File field also makes the fragment expose an
        /// <c>isAuthorizedForSave</c> input that the control's <c>[disabled]</c> binding reads.
        /// </summary>
        public FileUploadModel FileUpload { get; set; }

        /// <summary>
        /// The parent-collection relation name (this field's M2O <c>[WithMany]</c> value) when the field is a
        /// back-reference to a parent; null otherwise. When a composing parent passes a matching
        /// <c>hiddenParentRelation</c>, the field hides — the runtime, composition-friendly equivalent of the
        /// legacy "filter out the back-ref" rule (the child fragment stays reusable and context-free).
        /// </summary>
        public string ParentRelationName { get; set; }
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

    /// <summary>An Editor's image-upload arrow-fn that delegates to an ApiService upload method.</summary>
    internal sealed class EditorImageUploadModel
    {
        public string MethodName { get; set; }
        public string ApiMethodName { get; set; }
    }

    /// <summary>A File control's blob-upload method and its uploaded-event output.</summary>
    internal sealed class FileUploadModel
    {
        public string MethodName { get; set; }
        public string ApiMethodName { get; set; }
        public string OutputName { get; set; }
    }
}
