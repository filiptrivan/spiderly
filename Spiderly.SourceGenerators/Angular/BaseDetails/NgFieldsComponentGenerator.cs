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
                    .Select(f => $"    @Output() {f.FileUpload.OutputName} = new EventEmitter<SpiderlyFileSelectEvent>();")));
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

            string authInputBlock = (model.Fields.Any(f => f.FileUpload != null) || model.OrderedOneToManies.Count > 0)
                ? "\n    @Input() isAuthorizedForSave: boolean = false;"
                : "";

            string orderedInputs = string.Join("\n", model.OrderedOneToManies.SelectMany(o => new[]
            {
                $"    @Input() {o.PanelCollapsedInputName}: boolean = false;",
                $"    @Input() {o.AdditionalContentTemplateInputName}: TemplateRef<any> | undefined;",
            }));
            string orderedInputsBlock = orderedInputs.Length > 0 ? $"\n{orderedInputs}" : "";

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

            string tableFields = string.Join("\n", model.Tables.SelectMany(t => new[]
            {
                $"    {t.ColsFieldName}: Column<{t.ColsTypeArgument}>[];",
                $"    {t.PaginatedListFieldName} = {t.PaginatedListApiCall};",
                $"    {t.ExportFieldName} = {t.ExportApiCall};",
            }));
            string tableFieldsBlock = tableFields.Length > 0 ? $"\n{tableFields}" : "";

            // Re-indent the reused column literals (legacy emits them at 16 spaces for its deeper nesting) to one
            // level under this fragment's `async ngOnInit()` body. Content is preserved (TrimStart only).
            string colsInits = string.Join("\n", model.Tables.Select(t =>
                $"        this.{t.ColsFieldName} = [\n{string.Join(",\n", t.ColumnDefs.Select(d => "            " + d.TrimStart()))}\n        ];"));
            string ngOnInitBlock = model.Tables.Count > 0
                ? $"\n\n    async ngOnInit() {{\n{colsInits}\n    }}"
                : "";

            string fieldBlocks = string.Join("\n", model.Fields.Select(f => GetFieldBlock(f, model.MainDtoAccess)));
            string orderedBlocks = string.Join("\n", model.OrderedOneToManies.Select(GetOrderedOneToManyBlock));
            string tableBlocks = string.Join("\n", model.Tables.Select(GetTableBlock));
            // Join only the non-empty parts so an entity with no scalar fields (only ordered-O2M) doesn't emit a stray leading newline.
            string bodyBlocks = string.Join("\n", new[] { fieldBlocks, orderedBlocks, tableBlocks }.Where(b => b.Length > 0));

            List<string> componentImports = new()
            {
                "CommonModule", "FormsModule", "ReactiveFormsModule", "SpiderlyControlsModule", "TranslocoDirective",
            };
            if (model.OrderedOneToManies.Count > 0)
            {
                componentImports.Add("SpiderlyPanelsModule");
                componentImports.Add("IndexCardComponent");
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
    <ng-content select="[before]"></ng-content>
{{bodyBlocks}}
    <ng-content select="[after]"></ng-content>
</ng-container>
    `,
    imports: [
{{importsBlock}}
    ]
})
export class {{model.ComponentClassName}} {
    @Input() formGroup: SpiderlyFormGroup<{{model.SaveBodyTypeName}}>;
    @Input() config: {{model.ConfigClassName}} = {};{{relationInputBlock}}{{authInputBlock}}{{orderedInputsBlock}}{{outputsBlock}}{{optionsBlock}}{{tableFieldsBlock}}{{ctorBlock}}{{ngOnInitBlock}}{{searchMethodsBlock}}{{uploadMethodsBlock}}{{fileUploadMethodsBlock}}
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

        private static string GetOrderedOneToManyBlock(OrderedOneToManyModel o)
        {
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
                            <{{o.ChildFieldsSelector}} [formGroup]="{{o.ChildRowVar}}" [hiddenParentRelation]="'{{o.PropertyName}}'"></{{o.ChildFieldsSelector}}>
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
