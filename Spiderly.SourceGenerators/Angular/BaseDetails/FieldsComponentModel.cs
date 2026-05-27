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

        /// <summary>
        /// Many-to-many tables this entity renders as a spiderly-data-table. Complex-readonly tables are display-only;
        /// the editable lazy-load variant (added later) carries selection state. Columns are precomputed.
        /// </summary>
        public List<TableModel> Tables { get; set; } = new();

        /// <summary>
        /// [ComplexManyToManyList] junction collections this entity renders as a card panel of index-cards. Each card
        /// shows the related (other-side) entity's display name as its header and the junction's payload fields inline
        /// (no CRUD menu / add-button — rows are a fixed, default-seeded set). Distinct from <see cref="Tables"/> (a
        /// data-table) and <see cref="OrderedOneToManies"/> (composed child-entity forms).
        /// </summary>
        public List<ComplexManyToManyListModel> ComplexManyToManyLists { get; set; } = new();

        /// <summary>
        /// Distinct [UISection] names in first-appearance order over the entity's ordered property blocks, including a
        /// null entry for the implicit headerless section (unsectioned blocks). EMPTY when no property declares
        /// [UISection] — the fragment then renders flat. Drives the fragment's stacked section panels.
        /// </summary>
        public List<string> SectionOrder { get; set; } = new();
    }

    /// <summary>An [UIOrderedOneToMany] child collection rendered as a composed panel of index-cards.</summary>
    internal sealed class OrderedOneToManyModel
    {
        /// <summary>The parent's collection property (e.g. <c>"SegmentationItems"</c>). Also the value passed to the
        /// composed child fragment's <c>hiddenParentRelation</c> input to hide its back-ref M2O.</summary>
        public string PropertyName { get; set; }

        /// <summary>Transloco key for the panel header (today equals <see cref="PropertyName"/>).</summary>
        public string TranslationKey { get; set; }

        /// <summary>SpiderlyFormArray access expression, e.g. <c>formGroup.controls.orderedSegmentationItemsSaveBodyDTO</c>.</summary>
        public string FormArrayAccess { get; set; }

        /// <summary>The <c>@for</c> loop variable for one child row, e.g. <c>segmentationItemFormGroup</c>.</summary>
        public string ChildRowVar { get; set; }

        /// <summary>The composed child fragment's element selector, e.g. <c>segmentation-item-fields</c>.</summary>
        public string ChildFieldsSelector { get; set; }

        /// <summary>The composed child fragment's class name (for the component <c>imports</c> array), e.g. <c>SegmentationItemFieldsComponent</c>.</summary>
        public string ChildFieldsComponentClassName { get; set; }

        /// <summary>Transloco key for the add-row button, e.g. <c>AddNewSegmentationItem</c>.</summary>
        public string AddNewLabelKey { get; set; }

        /// <summary>Panel-collapsed <c>@Input()</c> name. The legacy <c>For{Entity}</c> suffix is intentionally
        /// dropped (the fragment is a standalone component, not a per-entity shell) — do not "restore" it.</summary>
        public string PanelCollapsedInputName { get; set; }

        /// <summary>Per-row additional-content <c>TemplateRef</c> <c>@Input()</c> name. The legacy <c>For{Entity}</c>
        /// suffix is intentionally dropped (see <see cref="PanelCollapsedInputName"/>).</summary>
        public string AdditionalContentTemplateInputName { get; set; }

        /// <summary>The [UISection] this block belongs to (null = the implicit headerless section).</summary>
        public string SectionName { get; set; }

        /// <summary>
        /// File-upload outputs re-exposed from the composed child fragment — one per File control on the child entity.
        /// The parent fragment declares each as its own @Output and, in the @for row, listens to the child's
        /// on{Prop}Uploaded and re-emits { event, formGroup } carrying the row's child DTO group (so a consumer can
        /// react to that row's upload, e.g. set sibling fields). The ordered-O2M equivalent of the legacy
        /// on{Prop}For{ChildEntity}Uploaded output. Empty when the child has no File control.
        /// </summary>
        public List<OrderedChildFileOutputModel> FileOutputs { get; set; } = new();
    }

    /// <summary>One File-upload output re-exposed by the parent fragment from a composed ordered-O2M child fragment.</summary>
    internal sealed class OrderedChildFileOutputModel
    {
        /// <summary>The parent fragment's @Output name, e.g. <c>onUrlForProductMediaUploaded</c>.</summary>
        public string ParentOutputName { get; set; }

        /// <summary>The composed child fragment's flat upload output to listen on, e.g. <c>onUrlUploaded</c>.</summary>
        public string ChildUploadOutputName { get; set; }

        /// <summary>The row's child DTO form-group access expression emitted as the event's <c>formGroup</c>, e.g.
        /// <c>productMediaFormGroup.controls.productMediaDTO</c> (scalar controls live on the DTO sub-group).</summary>
        public string RowDtoAccess { get; set; }
    }

    /// <summary>A [ComplexManyToManyList] junction collection rendered as a card panel of index-cards (one per row).</summary>
    internal sealed class ComplexManyToManyListModel
    {
        /// <summary>The parent's junction collection property (e.g. <c>"ProductVariantWarehouses"</c>).</summary>
        public string PropertyName { get; set; }

        /// <summary>Transloco key for the panel header (today equals <see cref="PropertyName"/>).</summary>
        public string TranslationKey { get; set; }

        /// <summary>SpiderlyFormArray access expression for the junction rows, e.g. <c>formGroup.controls.productVariantWarehouses</c>.</summary>
        public string FormArrayAccess { get; set; }

        /// <summary>The <c>@for</c> loop variable for one junction row, e.g. <c>productVariantWarehouseFormGroup</c>.</summary>
        public string JunctionRowVar { get; set; }

        /// <summary>Index-card header expression — the other-side entity's display name, e.g.
        /// <c>productVariantWarehouseFormGroup.getControl('warehouseDisplayName')?.getRawValue()</c>.</summary>
        public string HeaderExpression { get; set; }

        /// <summary>Panel-collapsed <c>@Input()</c> name. The legacy <c>For{Entity}</c> suffix is intentionally dropped
        /// (the fragment is a standalone component, not a per-entity shell) — do not "restore" it.</summary>
        public string PanelCollapsedInputName { get; set; }

        /// <summary>The junction's payload (non-relation, non-collection, generated) fields rendered inline in each card.</summary>
        public List<ComplexM2MJunctionFieldModel> JunctionFields { get; set; } = new();

        /// <summary>The [UISection] this block belongs to (null = the implicit headerless section).</summary>
        public string SectionName { get; set; }
    }

    /// <summary>One inline payload control inside a <see cref="ComplexManyToManyListModel"/> card.</summary>
    internal sealed class ComplexM2MJunctionFieldModel
    {
        /// <summary>Control element tag, e.g. <c>spiderly-number</c>.</summary>
        public string ControlTag { get; set; }

        /// <summary>Junction form-control name, e.g. <c>stock</c>.</summary>
        public string FormControlName { get; set; }

        /// <summary>Extra control attributes (with a leading space), e.g. <c> [decimal]="true" [maxFractionDigits]="2"</c>. Empty when none.</summary>
        public string ExtraControlAttributes { get; set; } = "";
    }

    /// <summary>A many-to-many spiderly-data-table (complex-readonly today; editable lazy-load later).</summary>
    internal sealed class TableModel
    {
        /// <summary>Transloco key for [tableTitle] (the parent collection property name).</summary>
        public string TranslationKey { get; set; }
        /// <summary>Cols field name, e.g. <c>rolesTableCols</c>. The legacy <c>For{Entity}</c> suffix is intentionally
        /// dropped from this and the observable-method field names below (the fragment is a standalone component, not
        /// a per-entity shell) — do not "restore" it. The reused column literals still call <c>For{Entity}</c>-suffixed
        /// ApiService methods, which is correct.</summary>
        public string ColsFieldName { get; set; }
        /// <summary>Child entity type for <c>Column&lt;T&gt;</c>, e.g. <c>Role</c>.</summary>
        public string ColsTypeArgument { get; set; }
        /// <summary>Precomputed column literals for the cols array (parity with the legacy table generator).</summary>
        public List<string> ColumnDefs { get; set; } = new();
        /// <summary>Paginated-list observable-method field name, e.g. <c>getPaginatedRolesListObservableMethod</c>.</summary>
        public string PaginatedListFieldName { get; set; }
        /// <summary>Its initializer call, e.g. <c>this.apiService.getPaginatedRolesListForUser</c>.</summary>
        public string PaginatedListApiCall { get; set; }
        /// <summary>Export observable-method field name, e.g. <c>exportRolesListToExcelObservableMethod</c>.</summary>
        public string ExportFieldName { get; set; }
        /// <summary>Its initializer call, e.g. <c>this.apiService.exportRolesListToExcelForUser</c>.</summary>
        public string ExportApiCall { get; set; }
        /// <summary>True for complex-readonly (no selection); the editable lazy-load variant sets false.</summary>
        public bool IsReadonly { get; set; }

        // --- editable lazy-load only (null/empty when IsReadonly) ---
        /// <summary>Mutable component field the data-table pushes newly-selected ids into, e.g. <c>newlySelectedRolesIds</c>.</summary>
        public string NewlySelectedField { get; set; }
        /// <summary>Mutable component field for unselected ids, e.g. <c>unselectedRolesIds</c>.</summary>
        public string UnselectedField { get; set; }
        /// <summary>Mutable component field for the select-all flag, e.g. <c>areAllRolesSelected</c>.</summary>
        public string AreAllSelectedField { get; set; }
        /// <summary>Mutable component field holding the last lazy-load filter, e.g. <c>lastRolesLazyLoadTableFilter</c>.</summary>
        public string LastFilterField { get; set; }
        /// <summary>SaveBody control name for selected ids, e.g. <c>selectedRolesIds</c> (no For{Entity}; backend contract).</summary>
        public string SelectedFormControl { get; set; }
        /// <summary>SaveBody control name for unselected ids, e.g. <c>unselectedRolesIds</c>.</summary>
        public string UnselectedFormControl { get; set; }
        /// <summary>SaveBody control name for the select-all flag, e.g. <c>areAllRolesSelected</c>.</summary>
        public string AreAllSelectedFormControl { get; set; }
        /// <summary>SaveBody control name for the table filter, e.g. <c>rolesTableFilter</c>.</summary>
        public string TableFilterFormControl { get; set; }
        /// <summary>Lazy-load arrow-fn name, e.g. <c>selectedRolesLazyLoadMethod</c>.</summary>
        public string LazyLoadMethodName { get; set; }
        /// <summary>Its ApiService call, e.g. <c>this.apiService.lazyLoadSelectedRolesIdsForUser</c>.</summary>
        public string LazyLoadApiCall { get; set; }
        /// <summary>Select-all-change handler name, e.g. <c>areAllRolesSelectedChange</c>.</summary>
        public string AreAllSelectedChangeMethodName { get; set; }
        /// <summary>On-lazy-load handler name, e.g. <c>onRolesLazyLoad</c>.</summary>
        public string OnLazyLoadMethodName { get; set; }
        /// <summary>Parent-id raw-value expression for the lazy-load filter, e.g. <c>this.formGroup.controls.userDTO.controls.id.getRawValue()</c> (the fragment has no modelId).</summary>
        public string ParentIdRawValueExpression { get; set; }

        /// <summary>The [UISection] this block belongs to (null = the implicit headerless section).</summary>
        public string SectionName { get; set; }
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

        /// <summary>The [UISection] this block belongs to (null = the implicit headerless section).</summary>
        public string SectionName { get; set; }
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
