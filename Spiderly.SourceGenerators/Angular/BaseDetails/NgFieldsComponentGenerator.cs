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
                .Select(f => $"    @Output() {f.ChangeOutput.OutputName} = new EventEmitter<{f.ChangeOutput.EventType}>();"));
            string outputsBlock = outputs.Length > 0 ? $"\n{outputs}" : "";

            return $$"""
@Component({
    selector: '{{model.Selector}}',
    template: `
<ng-container *transloco="let t">
    <ng-content select="[before]"></ng-content>
{{string.Join("\n", model.Fields.Select(GetFieldBlock))}}
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
    @Input() config: {{model.ConfigClassName}} = {};{{outputsBlock}}
}
""";
        }

        private static string GetFieldBlock(FieldModel field)
        {
            string eventAttr = field.ChangeOutput != null
                ? $" ({field.ChangeOutput.ControlEventName})=\"{field.ChangeOutput.OutputName}.next($event)\""
                : "";

            return $$"""
    <div *ngIf="config.{{field.ConfigShowFlagName}} !== false" class="{{field.Width}}">
        <{{field.ControlTag}} [control]="formGroup.getControl('{{field.FormControlName}}')"{{field.ExtraControlAttributes}}{{eventAttr}}></{{field.ControlTag}}>
        <ng-content select="[below{{field.PropertyName}}]"></ng-content>
    </div>
""";
        }

        internal static string BuildFieldsConfig(FieldsComponentModel model)
        {
            string flags = string.Join("\n", model.Fields.Select(f => $"    {f.ConfigShowFlagName}?: boolean;"));
            string body = flags.Length > 0 ? $"\n{flags}\n" : "\n";

            return $"export class {model.ConfigClassName} {{{body}}}";
        }
    }
}
