using System.Linq;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Emits the <c>{Entity}BaseDetails</c> shell component: panel chrome, Save/auth/return footer, the
    /// route -> modelId -> forkJoin(get MainUIFormDTO) -> mapMainUIFormToSaveBody -> initFormGroup lifecycle,
    /// and an embedded <c>{Entity}Fields</c> fragment bound to <c>parentFormGroup</c> + <c>fieldConfig</c>.
    /// Minimal single-panel scope; not yet wired into the generated output.
    /// </summary>
    internal static class NgShellComponentGenerator
    {
        internal static string BuildShellComponent(ShellComponentModel model)
        {
            string defaultAuthorized = model.DefaultAuthorized.ToString().ToLower();

            string additionalSaveAuthorization = string.Join("", model.AdditionalSavePermissionCodes.Select(c =>
                $"\n            (currentUserPermissionCodes.includes('{c.PermissionCode}') && this.modelId {(c.ForInsert ? "<= 0" : "> 0")}) ||"));

            // Existing-branch injection points (empty => byte-identical to the no-seed shell).
            string existingSeedForkJoinLines = string.Join("", model.SeedForkJoinParams
                .Select(p => $"\n                    {p},"));
            string orderedSeedAssignmentLines = string.Join("", model.OrderedChildSeedAssignments
                .Select(a => $"\n                    {a}"));

            string forwardedFileOutputsBlock = string.Join("", model.ForwardedFileOutputs
                .Select(name => $"\n    @Output() {name} = new EventEmitter<{{ event: SpiderlyFileSelectEvent; formGroup: SpiderlyFormGroup }}>();"));
            string forwardedFileOutputBindings = string.Join("", model.ForwardedFileOutputs
                .Select(name => $" ({name})=\"{name}.emit($event)\""));

            string newEntityBranch;
            if (model.SeedForkJoinParams.Count == 0)
            {
                newEntityBranch = $$"""
                this.baseFormService.initFormGroup(this.parentFormGroup, {{model.SaveBodyTypeName}});
                await this.handleAuthorizationForSave();
                this.loading = false;
                this.onAfterFormGroupInit.next();
""";
            }
            else
            {
                string newSeedForkJoinParams = string.Join("\n", model.SeedForkJoinParams
                    .Select(p => $"                    {p},"));
                string newEntityInitArg = model.NewEntitySeedInits.Count > 0
                    ? ", {\n" + string.Join(",\n", model.NewEntitySeedInits.Select(i => $"                        {i}")) + "\n                    }"
                    : "";
                newEntityBranch = $$"""
                forkJoin({
{{newSeedForkJoinParams}}
                })
                .subscribe(async (data) => {
                    this.baseFormService.initFormGroup(this.parentFormGroup, {{model.SaveBodyTypeName}}{{newEntityInitArg}});{{orderedSeedAssignmentLines}}
                    await this.handleAuthorizationForSave();
                    this.loading = false;
                    this.onAfterFormGroupInit.next();
                });
""";
            }

            return $$"""
@Component({
    selector: '{{model.Selector}}',
    template: `
<ng-container *transloco="let t">
    <spiderly-panel [isFirstMultiplePanel]="isFirstMultiplePanel" [isMiddleMultiplePanel]="isMiddleMultiplePanel" [isLastMultiplePanel]="isLastMultiplePanel" [showPanelHeader]="showPanelHeader" >
        <panel-header [title]="panelTitle" [showBigTitle]="showBigPanelTitle" [icon]="panelIcon"></panel-header>

        <panel-body>
            @defer (when loading === false) {
                <{{model.FieldsSelector}} [formGroup]="parentFormGroup" [config]="fieldConfig"{{forwardedFileOutputBindings}}>
                    <ng-content select="[before]" ngProjectAs="[before]"></ng-content>
                    <ng-content select="[after]" ngProjectAs="[after]"></ng-content>
                </{{model.FieldsSelector}}>
            } @placeholder {
                <card-skeleton [height]="502"></card-skeleton>
            }
        </panel-body>

        <panel-footer>
            <spiderly-button *ngIf="isAuthorizedForSave" (onClick)="save()" [label]="t('Save')" icon="pi pi-save"></spiderly-button>
            <ng-content select="[buttons]"></ng-content>
            <return-button *ngIf="showReturnButton" ></return-button>
        </panel-footer>
    </spiderly-panel>
</ng-container>
    `,
    imports: [
        CommonModule,
        FormsModule,
        ReactiveFormsModule,
        SpiderlyControlsModule,
        TranslocoDirective,
        CardSkeletonComponent,
        SpiderlyPanelsModule,
        {{model.FieldsComponentClassName}},
    ]
})
export class {{model.ComponentClassName}} {
    @Output() onSave = new EventEmitter<void>();
    @Output() onAfterFormGroupInit = new EventEmitter<void>();
    @Input() parentFormGroup: SpiderlyFormGroup<{{model.SaveBodyTypeName}}>;
    @Input() isFirstMultiplePanel: boolean = false;
    @Input() isMiddleMultiplePanel: boolean = false;
    @Input() isLastMultiplePanel: boolean = false;
    @Input() showPanelHeader: boolean = true;
    @Input() panelTitle: string;
    @Input() showBigPanelTitle: boolean = true;
    @Input() panelIcon: string;
    @Input() showReturnButton: boolean = true;
    @Input() fieldConfig: {{model.ConfigClassName}} = {};
    @Input() handleAdditionalSaveAuthorization: () => Promise<boolean> = () => Promise.resolve({{defaultAuthorized}});
    isAuthorizedForSave: boolean = {{defaultAuthorized}};
    @Output() onIsAuthorizedForSaveChange = new EventEmitter<IsAuthorizedForSaveEvent>();{{forwardedFileOutputsBlock}}

    modelId: number;
    loading: boolean = true;

    constructor(
        private apiService: ApiService,
        private route: ActivatedRoute,
        private baseFormService: BaseFormService,
        private authService: AuthService,
    ) {}

    ngOnInit(){
        this.parentFormGroup.saveObservableMethod = this.apiService.save{{model.EntityName}};

        this.route.params.subscribe(async (params) => {
            this.modelId = params['id'];

            if (this.modelId > 0) {
                forkJoin({
                    mainUIFormDTO: this.apiService.get{{model.EntityName}}MainUIFormDTO(this.modelId),{{existingSeedForkJoinLines}}
                })
                .subscribe(async (data) => {
                    const saveBody = this.baseFormService.mapMainUIFormToSaveBody(
                        {{model.MainUIFormTypeName}},
                        data.mainUIFormDTO,
                    );
                    this.baseFormService.initFormGroup(this.parentFormGroup, {{model.SaveBodyTypeName}}, saveBody);{{orderedSeedAssignmentLines}}
                    await this.handleAuthorizationForSave();
                    this.loading = false;
                    this.onAfterFormGroupInit.next();
                });
            }
            else {
{{newEntityBranch}}
            }
        });
    }

    handleAuthorizationForSave = async () => {
        const currentUserPermissionCodes = await firstValueFrom(this.authService.currentUserPermissionCodes$);
        const isAdditionallyAuthorizedForSave = await this.handleAdditionalSaveAuthorization();

        this.isAuthorizedForSave ={{additionalSaveAuthorization}}
            (currentUserPermissionCodes.includes('Insert{{model.EntityName}}') && this.modelId <= 0) ||
            (currentUserPermissionCodes.includes('Update{{model.EntityName}}') && this.modelId > 0) ||
            isAdditionallyAuthorizedForSave;

        if (this.isAuthorizedForSave) {
            this.parentFormGroup.enable();
        }
        else{
            this.parentFormGroup.disable();
        }

        this.onIsAuthorizedForSaveChange.next(
            new IsAuthorizedForSaveEvent({
                isAuthorizedForSave: this.isAuthorizedForSave,
            })
        );
    };

    save(){
        this.onSave.next();
    }
}
""";
        }
    }
}
