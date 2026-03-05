using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates Angular component code for displaying and editing entity details on the frontend.
    /// This generator targets C# entity and DTO classes (marked within 'Entities' or 'DTO' namespaces)
    /// and produces a TypeScript file (`{your-app-name}\Frontend\src\app\business\components\base-details.generated.ts`).
    /// </summary>
    [Generator]
    public class NgBaseDetailsGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            //#if DEBUG
            //            if (!Debugger.IsAttached)
            //            {
            //                Debugger.Launch();
            //            }
            //#endif
            var combined = PipelineFactory.CreatePipelineWithCallingPath(context,
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities, NamespaceExtensionCodes.DTO },
                new List<NamespaceExtensionCodes> { NamespaceExtensionCodes.Entities, NamespaceExtensionCodes.DTO });

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntitiesAndPath, config) = source;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgBaseDetailsGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> customDTOClasses = currentProjectClasses.Where(x => x.Namespace.EndsWith(".DTO")).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> referencedProjectEntities = referencedProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            if (currentProjectClasses == null || currentProjectClasses.Count == 0)
            {
                Console.WriteLine(currentProjectClasses.Count);
                return;
            }

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\components\base-details.generated.ts
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "components", "base-details.generated.ts");

            string result = $$"""
{{NgDetailsImportGenerator.GetImports(customDTOClasses, allEntities)}}

{{string.Join("\n\n", GetAngularBaseDetailsComponents(customDTOClasses, currentProjectEntities, allEntities))}}
""";

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static List<string> GetAngularBaseDetailsComponents(List<SpiderlyClass> customDTOClasses, List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyClass entity in currentProjectEntities
                .Where(x =>
                    x.HasUIDoNotGenerateAttribute() == false &&
                    x.IsReadonlyObject() == false &&
                    x.IsManyToMany() == false
                )
            )
            {
                result.Add($$"""
@Component({
    selector: '{{entity.Name.FromPascalToKebabCase()}}-base-details',
    template: `
<ng-container *transloco="let t">
    <spiderly-panel [isFirstMultiplePanel]="isFirstMultiplePanel" [isMiddleMultiplePanel]="isMiddleMultiplePanel" [isLastMultiplePanel]="isLastMultiplePanel" [showPanelHeader]="showPanelHeader" >
        <panel-header [title]="panelTitle" [showBigTitle]="showBigPanelTitle" [icon]="panelIcon"></panel-header>

        <panel-body>
            @defer (when loading === false) {
                <form class="spiderly-grid">
                    <ng-content select="[before]"></ng-content>
{{string.Join("\n", NgDetailsPropertyBlockGenerator.GetPropertyBlocks(entity.Properties.ToList(), entity, allEntities, customDTOClasses, isFromOrderedOneToMany: false))}}
                    <ng-content select="[after]"></ng-content>
                </form>
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
        IndexCardComponent,
        SpiderlyDataTableComponent,
        SpiderlyPanelsModule,
    ]
})
export class {{entity.Name}}BaseDetailsComponent {
    @Output() onSave = new EventEmitter<void>();
    @Output() onAfterFormGroupInit = new EventEmitter<void>();
    @Input() parentFormGroup: SpiderlyFormGroup<{{entity.Name}}SaveBody>;
    @Input() isFirstMultiplePanel: boolean = false;
    @Input() isMiddleMultiplePanel: boolean = false;
    @Input() isLastMultiplePanel: boolean = false;
    @Input() showPanelHeader: boolean = true;
    @Input() panelTitle: string;
    @Input() showBigPanelTitle: boolean = true;
    @Input() panelIcon: string;
    @Input() showReturnButton: boolean = true;
    @Input() handleAdditionalSaveAuthorization: () => Promise<boolean> = () => Promise.resolve({{(!Helpers.ShouldAuthorizeEntity(entity)).ToString().ToLower()}});
    isAuthorizedForSave: boolean = {{(!Helpers.ShouldAuthorizeEntity(entity)).ToString().ToLower()}};
    @Output() onIsAuthorizedForSaveChange = new EventEmitter<IsAuthorizedForSaveEvent>();
{{string.Join("\n", NgDetailsDataGenerator.GetBlobUploadedOutputVariables(entity.Properties, entity, allEntities, isFromOrderedOneToMany: false))}}

    modelId: number;
    loading: boolean = true;

{{string.Join("\n", NgDetailsVariableGenerator.GetOrderedOneToManyVariables(entity, allEntities))}}

{{string.Join("\n", NgDetailsVariableGenerator.GetComplexManyToManyListVariables(entity, allEntities))}}

{{string.Join("\n", NgDetailsVariableGenerator.GetPrimengOptionVariables(entity, allEntities, customDTOClasses))}}

{{string.Join("\n", NgDetailsVariableGenerator.GetManyToManyTableVariables(entity, allEntities))}}

{{string.Join("\n", NgDetailsVariableGenerator.GetSimpleManyToManyTableLazyLoadVariables(entity))}}

{{NgDetailsVariableGenerator.GetShowFormBlocksVariables(entity, allEntities, customDTOClasses)}}

{{NgDetailsVariableGenerator.GetHelperVariables(entity, allEntities, customDTOClasses)}}

    constructor(
        private apiService: ApiService,
        private route: ActivatedRoute,
        private baseFormService: BaseFormService,
        private validatorService: ValidatorService,
        private translocoService: TranslocoService,
        private authService: AuthService,
    ) {}

    ngOnInit(){
{{string.Join("\n", NgDetailsVariableGenerator.GetSimpleManyToManyTableLazyLoadSaveBodyAssignements(entity))}}

        this.parentFormGroup.saveObservableMethod = this.apiService.save{{entity.Name}};

        this.route.params.subscribe(async (params) => {
            this.modelId = params['id'];

{{string.Join("\n", NgDetailsDataGenerator.GetManyToManyMultiSelectListForDropdownMethods(entity, allEntities))}}
{{string.Join("\n", NgDetailsDataGenerator.GetManyToManyTableColsInitializations(entity, allEntities, customDTOClasses))}}

{{NgDetailsDataGenerator.GetEntityInitBlock(entity, allEntities)}}
        });
    }

    handleAuthorizationForSave = async () => {
        const currentUserPermissionCodes = await firstValueFrom(this.authService.currentUserPermissionCodes$);
        const isAdditionallyAuthorizedForSave = await this.handleAdditionalSaveAuthorization();

        this.isAuthorizedForSave =
{{NgDetailsVariableGenerator.GetAdditionalPermissionCodes(entity)}}
            (currentUserPermissionCodes.includes('Insert{{entity.Name}}') && this.modelId <= 0) ||
            (currentUserPermissionCodes.includes('Update{{entity.Name}}') && this.modelId > 0) ||
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

{{string.Join("\n", NgDetailsDataGenerator.GetSimpleManyToManyMethods(entity, allEntities))}}

{{string.Join("\n", NgDetailsDataGenerator.GetAutocompleteSearchMethods(entity, allEntities, customDTOClasses))}}

{{string.Join("\n", NgDetailsDataGenerator.GetUploadImageMethods(entity.Properties, entity, allEntities, isFromOrderedOneToMany: false))}}

    save(){
        this.onSave.next();
    }

}
""");
            }

            return result;
        }
    }
}
