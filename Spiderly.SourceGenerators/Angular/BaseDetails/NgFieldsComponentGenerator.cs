using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Emits the redesigned bare <c>{Entity}Fields</c> fragment and its <c>{Entity}FieldsConfig</c> class from a
    /// <see cref="FieldsComponentModel"/>. The fragment renders only field-grid blocks (no panel, no Save footer,
    /// no For{Entity} suffix); visibility is driven by the typed config object, and each field exposes a
    /// <c>below{Prop}</c> content slot. Not yet wired into the generated output.
    /// </summary>
    internal static class NgFieldsComponentGenerator
    {
        internal static string BuildFieldsComponent(FieldsComponentModel model)
        {
            string outputs = string.Join("\n", model.Fields
                .Where(f => f.ChangeOutput != null)
                .Select(f => $"    @Output() {f.ChangeOutput.OutputName} = new EventEmitter<{f.ChangeOutput.EventType}>();")
                .Concat(model.Fields
                    .Where(f => f.FileUpload != null)
                    .Select(f => $"    @Output() {f.FileUpload.OutputName} = new EventEmitter<SpiderlyFileSelectEvent>();"))
                .Concat(model.OrderedOneToManies
                    .SelectMany(o => o.FileOutputs)
                    .Select(fo => $"    @Output() {fo.ParentOutputName} = new EventEmitter<{{ event: SpiderlyFileSelectEvent; formGroup: SpiderlyFormGroup }}>();")));
            string outputsBlock = outputs.Length > 0 ? $"\n{outputs}" : "";

            string optionsFields = string.Join("\n", model.Fields
                .Where(f => f.OptionsFieldName != null)
                .Select(f => f.OptionsIsInput
                    ? $"    @Input() {f.OptionsFieldName}: Namebook[];"
                    : $"    {f.OptionsFieldName}: Namebook[];"));
            string optionsBlock = optionsFields.Length > 0 ? $"\n{optionsFields}" : "";

            string relationInputBlock = model.Fields.Any(f => f.ParentRelationName != null)
                ? "\n    @Input() hiddenParentRelation: string;"
                : "";

            string authInputBlock = (model.Fields.Any(f => f.FileUpload != null)
                || model.OrderedOneToManies.Count > 0
                || model.Tables.Any(t => !t.IsReadonly))
                ? "\n    @Input() isAuthorizedForSave: boolean = false;"
                : "";

            string orderedInputs = string.Join("\n", model.OrderedOneToManies.SelectMany(o => new[]
            {
                $"    @Input() {o.PanelCollapsedInputName}: boolean = false;",
                $"    @Input() {o.AdditionalContentTemplateInputName}: TemplateRef<any> | undefined;",
            }));
            string orderedInputsBlock = orderedInputs.Length > 0 ? $"\n{orderedInputs}" : "";

            string complexInputs = string.Join("\n", model.ComplexManyToManyLists
                .Select(c => $"    @Input() {c.PanelCollapsedInputName}: boolean = false;"));
            string complexInputsBlock = complexInputs.Length > 0 ? $"\n{complexInputs}" : "";

            bool needsApiService = model.Fields.Any(RequiresApiService) || model.Tables.Count > 0;
            List<string> ctorParams = new();
            if (needsApiService) ctorParams.Add("private apiService: ApiService");
            if (model.Tables.Count > 0) ctorParams.Add("private translocoService: TranslocoService");
            string ctorBlock = ctorParams.Count > 0 ? $"\n\n    constructor({string.Join(", ", ctorParams)}) {{}}" : "";

            string searchMethods = string.Join("\n\n", model.Fields
                .Where(f => f.Search != null)
                .Select(GetSearchMethod));
            string searchMethodsBlock = searchMethods.Length > 0 ? $"\n\n{searchMethods}" : "";

            string uploadMethods = string.Join("\n\n", model.Fields
                .Where(f => f.EditorImageUpload != null)
                .Select(GetEditorImageUploadMethod));
            string uploadMethodsBlock = uploadMethods.Length > 0 ? $"\n\n{uploadMethods}" : "";

            string fileUploadMethods = string.Join("\n\n", model.Fields
                .Where(f => f.FileUpload != null)
                .Select(GetFileUploadMethod));
            string fileUploadMethodsBlock = fileUploadMethods.Length > 0 ? $"\n\n{fileUploadMethods}" : "";

            string tableMethods = string.Join("\n\n", model.Tables.Where(t => !t.IsReadonly).Select(GetEditableTableMethods));
            string tableMethodsBlock = tableMethods.Length > 0 ? $"\n\n{tableMethods}" : "";

            string tableFields = string.Join("\n", model.Tables.SelectMany(t => new[]
            {
                $"    {t.ColsFieldName}: Column<{t.ColsTypeArgument}>[];",
                $"    {t.PaginatedListFieldName} = {t.PaginatedListApiCall};",
                $"    {t.ExportFieldName} = {t.ExportApiCall};",
            }));
            string tableFieldsBlock = tableFields.Length > 0 ? $"\n{tableFields}" : "";

            string tableSelectionFields = string.Join("\n", model.Tables.Where(t => !t.IsReadonly).SelectMany(t => new[]
            {
                $"    {t.NewlySelectedField}: number[] = [];",
                $"    {t.UnselectedField}: number[] = [];",
                $"    {t.AreAllSelectedField}: boolean = null;",
                $"    {t.LastFilterField}: Filter;",
            }));
            string tableSelectionFieldsBlock = tableSelectionFields.Length > 0 ? $"\n{tableSelectionFields}" : "";

            // Re-indent the reused column literals (legacy emits them at 16 spaces for its deeper nesting) to one
            // level under this fragment's `async ngOnInit()` body. Content is preserved (TrimStart only).
            string colsInits = string.Join("\n", model.Tables.Select(t =>
                $"        this.{t.ColsFieldName} = [\n{string.Join(",\n", t.ColumnDefs.Select(d => "            " + d.TrimStart()))}\n        ];"));
            string formWiring = string.Join("\n", model.Tables.Where(t => !t.IsReadonly).SelectMany(t => new[]
            {
                $"        this.formGroup.controls.{t.SelectedFormControl}.setValue(this.{t.NewlySelectedField});",
                $"        this.formGroup.controls.{t.UnselectedFormControl}.setValue(this.{t.UnselectedField});",
                $"        this.formGroup.controls.{t.AreAllSelectedFormControl}.setValue(this.{t.AreAllSelectedField});",
                $"        this.formGroup.controls.{t.TableFilterFormControl}.setValue(this.{t.LastFilterField});",
            }));
            string ngOnInitBody = string.Join("\n", new[] { colsInits, formWiring }.Where(s => s.Length > 0));
            string ngOnInitBlock = model.Tables.Count > 0
                ? $"\n\n    async ngOnInit() {{\n{ngOnInitBody}\n    }}"
                : "";

            string innerBody;
            if (model.SectionOrder.Count == 0)
            {
                string fieldBlocks = string.Join("\n", model.Fields.Select(f => GetFieldBlock(f, model.MainDtoAccess)));
                string orderedBlocks = string.Join("\n", model.OrderedOneToManies.Select(GetOrderedOneToManyBlock));
                string complexBlocks = string.Join("\n", model.ComplexManyToManyLists.Select(GetComplexManyToManyListBlock));
                string tableBlocks = string.Join("\n", model.Tables.Select(GetTableBlock));
                // Join only the non-empty parts so an entity with no scalar fields (only ordered-O2M) doesn't emit a stray leading newline.
                string flat = string.Join("\n", new[] { fieldBlocks, orderedBlocks, complexBlocks, tableBlocks }.Where(b => b.Length > 0));
                innerBody = $"    <ng-content select=\"[before]\"></ng-content>\n{flat}\n    <ng-content select=\"[after]\"></ng-content>";
            }
            else
            {
                // Sectioned: GetSectionPanel re-iterates the block collections per section, so the flat-mode joins above aren't needed here.
                innerBody = string.Join("\n", model.SectionOrder.Select((section, i) => GetSectionPanel(model, section, i, model.SectionOrder.Count)));
            }

            List<string> componentImports = new()
            {
                "CommonModule", "FormsModule", "ReactiveFormsModule", "SpiderlyControlsModule", "TranslocoDirective",
            };
            if (model.OrderedOneToManies.Count > 0 || model.SectionOrder.Count > 0 || model.ComplexManyToManyLists.Count > 0)
            {
                componentImports.Add("SpiderlyPanelsModule");
                if (model.OrderedOneToManies.Count > 0 || model.ComplexManyToManyLists.Count > 0)
                    componentImports.Add("IndexCardComponent");
                if (model.OrderedOneToManies.Count > 0)
                    componentImports.AddRange(model.OrderedOneToManies.Select(o => o.ChildFieldsComponentClassName).Distinct());
            }
            if (model.Tables.Count > 0)
                componentImports.Add("SpiderlyDataTableComponent");
            string importsBlock = string.Join("\n", componentImports.Select(i => $"        {i},"));

            return $$"""
@Component({
    selector: '{{model.Selector}}',
    template: `
<ng-container *transloco="let t">
{{innerBody}}
</ng-container>
    `,
    imports: [
{{importsBlock}}
    ]
})
export class {{model.ComponentClassName}} {
    @Input() formGroup: SpiderlyFormGroup<{{model.SaveBodyTypeName}}>;
    @Input() config: {{model.ConfigClassName}} = {};{{relationInputBlock}}{{authInputBlock}}{{orderedInputsBlock}}{{complexInputsBlock}}{{outputsBlock}}{{optionsBlock}}{{tableFieldsBlock}}{{tableSelectionFieldsBlock}}{{ctorBlock}}{{ngOnInitBlock}}{{searchMethodsBlock}}{{uploadMethodsBlock}}{{fileUploadMethodsBlock}}{{tableMethodsBlock}}
}
""";
        }

        /// <summary>True when a field needs the injected ApiService (autocomplete search or a blob/image upload).</summary>
        private static bool RequiresApiService(FieldModel field) =>
            field.Search != null || field.EditorImageUpload != null || field.FileUpload != null;

        private static string GetSearchMethod(FieldModel field)
        {
            return $$"""
    {{field.Search.MethodName}}(event: AutoCompleteCompleteEvent, modelId: number = null) {
        this.apiService.{{field.Search.ApiMethodName}}(50, event?.query ?? '', modelId).subscribe(no => {
            this.{{field.Search.OptionsFieldName}} = no;
        });
    }
""";
        }

        private static string GetEditorImageUploadMethod(FieldModel field)
        {
            return $$"""
    {{field.EditorImageUpload.MethodName}} = (formData: FormData): Observable<EditorImageUploadResult> => {
        return this.apiService.{{field.EditorImageUpload.ApiMethodName}}(formData);
    }
""";
        }

        private static string GetFileUploadMethod(FieldModel field)
        {
            return $$"""
    {{field.FileUpload.MethodName}}(event: SpiderlyFileSelectEvent, formGroup: SpiderlyFormGroup){
        this.apiService.{{field.FileUpload.ApiMethodName}}(event.formData).subscribe((completeFileName: string) => {
            formGroup.controls['{{field.FormControlName}}'].setValue(completeFileName);
            this.{{field.FileUpload.OutputName}}.emit(event);
        });
    }
""";
        }

        private static string GetFieldBlock(FieldModel field, string mainDtoAccess)
        {
            string controlBase = field.BindsOnSaveBody ? "formGroup" : mainDtoAccess;

            string eventAttr = field.ChangeOutput != null
                ? $" ({field.ChangeOutput.ControlEventName})=\"{field.ChangeOutput.OutputName}.next($event)\""
                : "";

            string relationGuard = field.ParentRelationName != null
                ? $" && hiddenParentRelation !== '{field.ParentRelationName}'"
                : "";

            return $$"""
    <div *ngIf="config.{{field.ConfigShowFlagName}} !== false{{relationGuard}}" class="{{field.Width}}">
        <{{field.ControlTag}} [control]="{{controlBase}}.getControl('{{field.FormControlName}}')"{{field.ExtraControlAttributes}}{{eventAttr}}></{{field.ControlTag}}>
        <ng-content select="[below{{field.PropertyName}}]"></ng-content>
    </div>
""";
        }

        private static string GetTableBlock(TableModel t)
        {
            if (t.IsReadonly)
            {
                return $$"""
    <div class="col-8">
        <spiderly-data-table
            [tableTitle]="t('{{t.TranslationKey}}')"
            [cols]="{{t.ColsFieldName}}"
            [getPaginatedListObservableMethod]="{{t.PaginatedListFieldName}}"
            [exportListToExcelObservableMethod]="{{t.ExportFieldName}}"
            [showAddButton]="false"
            [readonly]="true"></spiderly-data-table>
        <ng-content select="[below{{t.TranslationKey}}]"></ng-content>
    </div>
""";
            }

            return $$"""
    <div class="col-8">
        <spiderly-data-table
            [tableTitle]="t('{{t.TranslationKey}}')"
            [cols]="{{t.ColsFieldName}}"
            [getPaginatedListObservableMethod]="{{t.PaginatedListFieldName}}"
            [exportListToExcelObservableMethod]="{{t.ExportFieldName}}"
            [showAddButton]="false"
            [readonly]="!isAuthorizedForSave"
            selectionMode="multiple"
            [newlySelectedItems]="{{t.NewlySelectedField}}"
            [unselectedItems]="{{t.UnselectedField}}"
            [rows]="5"
            (onLazyLoad)="{{t.OnLazyLoadMethodName}}($event)"
            [selectedLazyLoadObservableMethod]="{{t.LazyLoadMethodName}}"
            (onIsAllSelectedChange)="{{t.AreAllSelectedChangeMethodName}}($event)"></spiderly-data-table>
        <ng-content select="[below{{t.TranslationKey}}]"></ng-content>
    </div>
""";
        }

        private static string GetEditableTableMethods(TableModel t)
        {
            return $$"""
    {{t.LazyLoadMethodName}} = (event: Filter): Observable<LazyLoadSelectedIdsResult> => {
        let filter: Filter = event;
        filter.additionalFilterIdLong = {{t.ParentIdRawValueExpression}};

        return {{t.LazyLoadApiCall}}(filter);
    }

    {{t.AreAllSelectedChangeMethodName}}(event: AllClickEvent){
        this.{{t.AreAllSelectedField}} = event.checked;
    }

    {{t.OnLazyLoadMethodName}}(event: Filter){
        this.{{t.LastFilterField}} = event;
    }
""";
        }

        private static string GetOrderedOneToManyBlock(OrderedOneToManyModel o)
        {
            string fileOutputBindings = string.Join("", o.FileOutputs
                .Select(fo => $" ({fo.ChildUploadOutputName})=\"{fo.ParentOutputName}.emit({{ event: $event, formGroup: {fo.RowDtoAccess} }})\""));

            return $$"""
    <div class="col-8">
        <spiderly-panel [toggleable]="true" [collapsed]="{{o.PanelCollapsedInputName}}">
            <panel-header [title]="t('{{o.TranslationKey}}')" icon="pi pi-list"></panel-header>
            <panel-body [normalBottomPadding]="true">
                @for ({{o.ChildRowVar}} of {{o.FormArrayAccess}}.getFormGroups(); track {{o.ChildRowVar}}.trackingId; let index = $index; let last = $last) {
                    <index-card
                    [index]="index"
                    [last]="false"
                    [crudMenu]="{{o.FormArrayAccess}}.getCrudMenuForOrderedData()"
                    [showCrudMenu]="isAuthorizedForSave"
                    (onMenuIconClick)="{{o.FormArrayAccess}}.lastMenuIconIndexClicked = $event"
                    >
                        <form [formGroup]="{{o.ChildRowVar}}" class="spiderly-grid">
                            <{{o.ChildFieldsSelector}} [formGroup]="{{o.ChildRowVar}}" [hiddenParentRelation]="'{{o.PropertyName}}'"{{fileOutputBindings}}></{{o.ChildFieldsSelector}}>
                            <ng-container *ngIf="{{o.AdditionalContentTemplateInputName}}">
                                <ng-container *ngTemplateOutlet="{{o.AdditionalContentTemplateInputName}}; context: { $implicit: {{o.ChildRowVar}}, formGroup: {{o.ChildRowVar}}, index: index, last: last }"></ng-container>
                            </ng-container>
                        </form>
                    </index-card>
                }

                <div class="panel-add-button">
                    <spiderly-button [disabled]="!isAuthorizedForSave" (onClick)="{{o.FormArrayAccess}}.addNewFormGroup(null)" [label]="t('{{o.AddNewLabelKey}}')" icon="pi pi-plus"></spiderly-button>
                </div>

            </panel-body>
        </spiderly-panel>
    </div>
""";
        }

        private static string GetComplexManyToManyListBlock(ComplexManyToManyListModel c)
        {
            string fieldsBlock = string.Join("\n", c.JunctionFields.Select(f => $$"""
                            <div class="col-8">
                                <{{f.ControlTag}} [control]="{{c.JunctionRowVar}}.getControl('{{f.FormControlName}}')"{{f.ExtraControlAttributes}}></{{f.ControlTag}}>
                            </div>
"""));

            return $$"""
    <div class="col-8">
        <spiderly-panel [toggleable]="true" [collapsed]="{{c.PanelCollapsedInputName}}">
            <panel-header [title]="t('{{c.TranslationKey}}')" icon="pi pi-list"></panel-header>
            <panel-body [normalBottomPadding]="true">
                @for ({{c.JunctionRowVar}} of {{c.FormArrayAccess}}.getFormGroups(); track {{c.JunctionRowVar}}.trackingId; let index = $index) {
                    <index-card
                    [index]="index"
                    [last]="false"
                    [header]="{{c.HeaderExpression}}"
                    [showCrudMenu]="false"
                    >
                        <form [formGroup]="{{c.JunctionRowVar}}" class="spiderly-grid">
{{fieldsBlock}}
                        </form>
                    </index-card>
                }

            </panel-body>
        </spiderly-panel>
    </div>
""";
        }

        private static string GetSectionPanel(FieldsComponentModel model, string section, int index, int count)
        {
            bool isOnly = count == 1;
            bool isFirst = !isOnly && index == 0;
            bool isLast = !isOnly && index == count - 1;
            bool isMiddle = !isOnly && !isFirst && !isLast;
            bool hasHeader = section != null;

            string header = hasHeader
                ? $"\n        <panel-header [title]=\"t('{section}')\" [showBigTitle]=\"false\"></panel-header>"
                : "";

            string sectionBlocks = string.Join("\n", new[]
            {
                string.Join("\n", model.Fields.Where(f => f.SectionName == section).Select(f => GetFieldBlock(f, model.MainDtoAccess))),
                string.Join("\n", model.OrderedOneToManies.Where(o => o.SectionName == section).Select(GetOrderedOneToManyBlock)),
                string.Join("\n", model.ComplexManyToManyLists.Where(c => c.SectionName == section).Select(GetComplexManyToManyListBlock)),
                string.Join("\n", model.Tables.Where(t => t.SectionName == section).Select(GetTableBlock)),
            }.Where(b => b.Length > 0));

            string before = index == 0 ? "    <ng-content select=\"[before]\"></ng-content>\n" : "";
            string after = index == count - 1 ? "\n    <ng-content select=\"[after]\"></ng-content>" : "";

            return $$"""
    <spiderly-panel [isFirstMultiplePanel]="{{isFirst.ToString().ToLower()}}" [isMiddleMultiplePanel]="{{isMiddle.ToString().ToLower()}}" [isLastMultiplePanel]="{{isLast.ToString().ToLower()}}" [showPanelHeader]="{{hasHeader.ToString().ToLower()}}">{{header}}
        <panel-body>
            <form class="spiderly-grid">
{{before}}{{sectionBlocks}}{{after}}
            </form>
        </panel-body>
    </spiderly-panel>
""";
        }

        internal static string BuildFieldsConfig(FieldsComponentModel model)
        {
            string flags = string.Join("\n", model.Fields
                .SelectMany(f => f.ExtraConfigFlags.Prepend(f.ConfigShowFlagName))
                .Select(name => $"    {name}?: boolean;"));
            string body = flags.Length > 0 ? $"\n{flags}\n" : "\n";

            return $"export class {model.ConfigClassName} {{{body}}}";
        }
    }
}
