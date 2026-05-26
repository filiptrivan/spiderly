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

            string authInputBlock = model.Fields.Any(f => f.FileUpload != null)
                ? "\n    @Input() isAuthorizedForSave: boolean = false;"
                : "";

            string ctorBlock = model.Fields.Any(RequiresApiService)
                ? "\n\n    constructor(private apiService: ApiService) {}"
                : "";

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

            return $$"""
@Component({
    selector: '{{model.Selector}}',
    template: `
<ng-container *transloco="let t">
    <ng-content select="[before]"></ng-content>
{{string.Join("\n", model.Fields.Select(f => GetFieldBlock(f, model.MainDtoAccess)))}}
    <ng-content select="[after]"></ng-content>
</ng-container>
    `,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        SpiderlyControlsModule,
        TranslocoDirective,
    ]
})
export class {{model.ComponentClassName}} {
    @Input() formGroup: SpiderlyFormGroup<{{model.SaveBodyTypeName}}>;
    @Input() config: {{model.ConfigClassName}} = {};{{authInputBlock}}{{outputsBlock}}{{optionsBlock}}{{ctorBlock}}{{searchMethodsBlock}}{{uploadMethodsBlock}}{{fileUploadMethodsBlock}}
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

            return $$"""
    <div *ngIf="config.{{field.ConfigShowFlagName}} !== false" class="{{field.Width}}">
        <{{field.ControlTag}} [control]="{{controlBase}}.getControl('{{field.FormControlName}}')"{{field.ExtraControlAttributes}}{{eventAttr}}></{{field.ControlTag}}>
        <ng-content select="[below{{field.PropertyName}}]"></ng-content>
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
