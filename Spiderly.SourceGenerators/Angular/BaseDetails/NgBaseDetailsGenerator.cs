using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
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
    //
    // ---------------------------------------------------------------------------
    // REDESIGN ATTEMPT — paused 2026-05 (the experimental code was removed; see git history).
    // We tried replacing this flattened mega-component generator with a bare {Entity}Fields
    // fragment (own fields, config-driven visibility) + an {Entity}BaseDetails shell that
    // COMPOSES child fragments instead of flattening nested entities, built inert behind a
    // characterization-snapshot net.
    //
    // Why we paused (lessons):
    //  - Framework-internal refactor with only LATERAL payoff for the consuming app (same admin
    //    screens, cleaner internals — no user/revenue impact).
    //  - The remaining switchover was the riskiest part (removes the net, spans two repos,
    //    touches ~23 consumer pages) for that lateral gain.
    //  - The shell-drives-fragment surface kept growing past scope: dropdown/multiselect option
    //    loading, [isAuthorizedForSave] binding, below-slot forwarding (ng-content can't reach
    //    @for rows -> needs TemplateRef), sectioned-shell double-panel coordination. "Almost
    //    done" was repeatedly unreliable.
    //  - Net result was MORE generator code/concepts (model+builder+emitter x2) — justified for
    //    testability, but not obviously simpler than this.
    //
    // The architecture (compose > flatten, config-driven, snapshot-tested) is sound. Revisit only
    // with a concrete driver: a Spiderly release, or real maintenance pain in the flattened output
    // for large entities (e.g. Product).
    // ---------------------------------------------------------------------------
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
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO },
                new List<ClassCategoryCodes> { ClassCategoryCodes.Entities, ClassCategoryCodes.DTO });

            var combinedWithEnums = combined.Combine(PipelineFactory.GetSpiderlyEnumNamesProvider(context.SyntaxProvider));

            context.RegisterSafeImplementationSourceOutput(combinedWithEnums, static (spc, source) =>
            {
                var (combinedSource, enumNames) = source;
                var (classesAndEntitiesAndPath, config) = combinedSource;
                var (classesAndEntities, callingPath) = classesAndEntitiesAndPath;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, enumNames, callingPath, config, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, ImmutableArray<string> spiderlyEnumNames, string callingProjectDirectory, SpiderlyConfig config, SourceProductionContext context)
        {
            if (classes.Count == 0)
                return;

            if (!config.IsGeneratorEnabled(nameof(NgBaseDetailsGenerator)))
                return;

            List<SpiderlyClass> currentProjectClasses = SpiderlyClassFactory.GetSpiderlyClasses(classes, referencedProjectClasses, spiderlyEnumNames);

            if (currentProjectClasses.Count == 0)
                return;

            List<SpiderlyClass> customDTOClasses = currentProjectClasses.Where(x => x.HasSpiderlyDTOAttribute()).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> referencedProjectEntities = referencedProjectClasses.Where(x => x.HasSpiderlyEntityAttribute()).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\components\base-details.generated.ts

            // Intentionally ONE fixed output file, not one file per entity.
            // These generators StreamWriter to disk (not Roslyn AddSource), so output is NOT ephemeral:
            // a per-entity scheme would leave an orphaned `{old-entity}.base-details.generated.ts` behind on
            // every entity rename/delete, and users forget to clean those up. Overwriting a single fixed path
            // is atomic and orphan-free by construction. The navigation cost of a large file is accepted in
            // exchange for that guarantee. Do not split this per entity without first moving emission off the
            // source generator onto a build step that owns (and can prune) the output directory.
            string rootPath = callingProjectDirectory.GetRootPath();
            string outputPath = Path.Combine(rootPath, "Frontend", "src", "app", "business", "components", "base-details.generated.ts");

            string result = $$"""
{{NgDetailsImportGenerator.GetImports(customDTOClasses, allEntities, currentProjectEntities)}}

{{string.Join("\n\n", GetAngularBaseDetailsComponents(customDTOClasses, currentProjectEntities, allEntities))}}
""";

            Helpers.WriteToTheFile(result, outputPath);
        }

        private static List<string> GetAngularBaseDetailsComponents(List<SpiderlyClass> customDTOClasses, List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyClass entity in currentProjectEntities.Where(x => x.GeneratesDetailsComponent()))
            {
                result.Add($$"""
@Component({
    selector: '{{entity.Name.FromPascalToKebabCase()}}-base-details',
    template: `
<ng-container *transloco="let t">
{{GetDetailsPanels(entity, allEntities, customDTOClasses)}}
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
{{string.Join("\n", NgDetailsDataGenerator.GetEnumDropdownOptionsInitializations(entity, allEntities, customDTOClasses))}}
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

        /// <summary>
        /// Renders the details panel region. When no property declares <c>[UIDetailsGroup]</c> this emits
        /// the original single flat-grid panel (backward compatible). Otherwise it emits one stacked panel
        /// per group, with the Save footer on the last panel only.
        /// </summary>
        private static string GetDetailsPanels(SpiderlyClass entity, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses)
        {
            if (NgDetailsPropertyBlockGenerator.HasAnyUISection(entity.Properties.ToList(), entity, customDTOClasses) == false)
                return GetSingleDetailsPanel(entity, allEntities, customDTOClasses);

            return GetGroupedDetailsPanels(entity, allEntities, customDTOClasses);
        }

        private static string GetSingleDetailsPanel(SpiderlyClass entity, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses)
        {
            return $$"""
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
""";
        }

        private static string GetGroupedDetailsPanels(SpiderlyClass entity, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses)
        {
            List<DetailsFieldGroup> groups = NgDetailsPropertyBlockGenerator.GetGroupedPropertyBlocks(entity.Properties.ToList(), entity, allEntities, customDTOClasses);

            List<string> panels = new();

            for (int i = 0; i < groups.Count; i++)
            {
                DetailsFieldGroup group = groups[i];

                bool isOnly = groups.Count == 1;
                bool isFirst = isOnly == false && i == 0;
                bool isLast = isOnly == false && i == groups.Count - 1;
                bool isMiddle = isOnly == false && isFirst == false && isLast == false;
                bool showHeader = group.TranslationKey != null;

                string header = showHeader
                    ? $$"""
        <panel-header [title]="t('{{group.TranslationKey}}')" [showBigTitle]="false"></panel-header>
"""
                    : "";

                string beforeSlot = i == 0 ? "                    <ng-content select=\"[before]\"></ng-content>\n" : "";
                string afterSlot = i == groups.Count - 1 ? "\n                    <ng-content select=\"[after]\"></ng-content>" : "";

                string footer = i == groups.Count - 1
                    ? $$"""

        <panel-footer>
            <spiderly-button *ngIf="isAuthorizedForSave" (onClick)="save()" [label]="t('Save')" icon="pi pi-save"></spiderly-button>
            <ng-content select="[buttons]"></ng-content>
            <return-button *ngIf="showReturnButton" ></return-button>
        </panel-footer>
"""
                    : "";

                panels.Add($$"""
    <spiderly-panel [isFirstMultiplePanel]="{{isFirst.ToString().ToLower()}}" [isMiddleMultiplePanel]="{{isMiddle.ToString().ToLower()}}" [isLastMultiplePanel]="{{isLast.ToString().ToLower()}}" [showPanelHeader]="{{showHeader.ToString().ToLower()}}" >
{{header}}
        <panel-body>
            @defer (when loading === false) {
                <form class="spiderly-grid">
{{beforeSlot}}{{string.Join("\n", group.Blocks)}}{{afterSlot}}
                </form>
            } @placeholder {
                <card-skeleton [height]="200"></card-skeleton>
            }
        </panel-body>
{{footer}}
    </spiderly-panel>
""");
            }

            return string.Join("\n", panels);
        }
    }
}
