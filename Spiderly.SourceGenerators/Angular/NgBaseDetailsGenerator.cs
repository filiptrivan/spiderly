using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Spiderly.SourceGenerators.Angular;
using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Shared;
using Spiderly.SourceGenerators.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    /// <summary>
    /// Generates Angular component code for displaying and editing entity details on the frontend.
    /// This generator targets C# entity and DTO classes (marked within 'Entities' or 'DTO' namespaces)
    /// and produces a TypeScript file (`{your-app-name}\Frontend\src\app\business\components\base-details\{your-app-name}-base-details.generated.ts`).
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
            IncrementalValuesProvider<ClassDeclarationSyntax> classDeclarations = Helpers.GetClassIncrementalValuesProvider(context.SyntaxProvider, new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO
                });

            IncrementalValueProvider<List<SpiderlyClass>> referencedProjectClasses = Helpers.GetIncrementalValueProviderClassesFromReferencedAssemblies(context,
                new List<NamespaceExtensionCodes>
                {
                    NamespaceExtensionCodes.Entities,
                    NamespaceExtensionCodes.DTO,
                });

            IncrementalValueProvider<string> callingProjectDirectory = context.GetCallingPath();

            var combined = classDeclarations.Collect()
                .Combine(referencedProjectClasses)
                .Combine(callingProjectDirectory);

            context.RegisterImplementationSourceOutput(combined, static (spc, source) =>
            {
                var (classesAndEntities, callingPath) = source;
                var (classes, referencedClasses) = classesAndEntities;

                Execute(classes, referencedClasses, callingPath, spc);
            });
        }

        private static void Execute(IList<ClassDeclarationSyntax> classes, List<SpiderlyClass> referencedProjectClasses, string callingProjectDirectory, SourceProductionContext context)
        {
            if (classes.Count <= 1) // 1 because of config settings
                return;

            List<SpiderlyClass> currentProjectClasses = Helpers.GetSpiderlyClasses(classes, referencedProjectClasses);
            List<SpiderlyClass> customDTOClasses = currentProjectClasses.Where(x => x.Namespace.EndsWith(".DTO")).ToList();
            List<SpiderlyClass> currentProjectEntities = currentProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> referencedProjectEntities = referencedProjectClasses.Where(x => x.Namespace.EndsWith(".Entities")).ToList();
            List<SpiderlyClass> allEntities = currentProjectEntities.Concat(referencedProjectEntities).ToList();

            string namespaceValue = currentProjectClasses[0].Namespace;
            string projectName = Helpers.GetProjectName(namespaceValue);

            string outputPath =
                Helpers.GetGeneratorOutputPath(nameof(NgBaseDetailsGenerator), currentProjectClasses) ??
                // ...\Backend\PlayertyLoyals.Business -> ...\Frontend\src\app\business\components\base-details\{projectName}.ts
                callingProjectDirectory.ReplaceEverythingAfter(@"\Backend\", $@"\Frontend\src\app\business\components\base-details\{projectName.FromPascalToKebabCase()}-base-details.generated.ts");

            string result = $$"""
{{GetImports(customDTOClasses, allEntities)}}

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
                <form class="grid">
                    <ng-content select="[BEFORE]"></ng-content>
{{string.Join("\n", GetPropertyBlocks(entity.Properties.ToList(), entity, allEntities, customDTOClasses, isFromOrderedOneToMany: false))}}
                    <ng-content select="[AFTER]"></ng-content>
                </form>
            } @placeholder {
                <card-skeleton [height]="502"></card-skeleton>
            }
        </panel-body>

        <panel-footer>
            <spiderly-button *ngIf="isAuthorizedForSave" (onClick)="save()" [label]="t('Save')" icon="pi pi-save"></spiderly-button>
            @for (button of additionalButtons; track button.label) {
                <spiderly-button (onClick)="button.onClick()" [disabled]="button.disabled" [label]="button.label" [icon]="button.icon"></spiderly-button>
            }
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
    @Input() getCrudMenuForOrderedData: (formArray: SpiderlyFormArray, modelConstructor: BaseEntity, lastMenuIconIndexClicked: LastMenuIconIndexClicked, adjustFormArrayManually: boolean) => MenuItem[];
    @Input() formGroup: SpiderlyFormGroup;
    @Input() {{entity.Name.FirstCharToLower()}}FormGroup: SpiderlyFormGroup<{{entity.Name}}>;
    @Input() additionalButtons: SpiderlyButton[] = [];
    @Input() isFirstMultiplePanel: boolean = false;
    @Input() isMiddleMultiplePanel: boolean = false;
    @Input() isLastMultiplePanel: boolean = false;
    @Input() showPanelHeader: boolean = true;
    @Input() panelTitle: string;
    @Input() showBigPanelTitle: boolean = true;
    @Input() panelIcon: string;
    @Input() showReturnButton: boolean = true;
    authorizationForSaveSubscription: Subscription;
    @Input() authorizedForSaveObservable: () => Observable<boolean> = () => of({{(!Helpers.ShouldAuthorizeEntity(entity)).ToString().ToLower()}});
    isAuthorizedForSave: boolean = {{(!Helpers.ShouldAuthorizeEntity(entity)).ToString().ToLower()}};
    @Output() onIsAuthorizedForSaveChange = new EventEmitter<IsAuthorizedForSaveEvent>(); 

    modelId: number;
    loading: boolean = true;

    {{entity.Name.FirstCharToLower()}}SaveBodyName: string = nameof<{{entity.Name}}SaveBody>('{{entity.Name.FirstCharToLower()}}DTO');

{{string.Join("\n\n", GetOrderedOneToManyVariables(entity, allEntities))}}

{{string.Join("\n", GetPrimengOptionVariables(entity.Properties, entity, allEntities))}}

{{string.Join("\n", GetCustomSpiderlyFormControls(entity))}}

{{string.Join("\n", GetSimpleManyToManyTableLazyLoadVariables(entity, allEntities))}}

{{GetShowFormBlocksVariables(entity, allEntities, customDTOClasses)}}

{{GetHelperVariables(entity, allEntities, customDTOClasses)}}

    constructor(
        private apiService: ApiService,
        private route: ActivatedRoute,
        private baseFormService: BaseFormService,
        private validatorService: ValidatorService,
        private translateLabelsService: TranslateLabelsService,
        private translocoService: TranslocoService,
        private authService: AuthService,
    ) {}

    ngOnInit(){
        this.formGroup.initSaveBody = () => { 
            let saveBody = new {{entity.Name}}SaveBody();
            saveBody.{{entity.Name.FirstCharToLower()}}DTO = this.{{entity.Name.FirstCharToLower()}}FormGroup.getRawValue();
{{string.Join("\n", GetOrderedOneToManySaveBodyAssignements(entity, allEntities))}}
{{string.Join("\n", GetManyToManyMultiSelectSaveBodyAssignements(entity))}}
{{string.Join("\n", GetManyToManyMultiAutocompleteSaveBodyAssignements(entity))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoadSaveBodyAssignements(entity))}}
            return saveBody;
        }

        this.formGroup.saveObservableMethod = this.apiService.save{{entity.Name}};
        this.formGroup.mainDTOName = this.{{entity.Name.FirstCharToLower()}}SaveBodyName;

        this.route.params.subscribe(async (params) => {
            this.modelId = params['id'];

{{string.Join("\n", GetManyToManyMultiSelectListForDropdownMethods(entity, allEntities))}}
{{string.Join("\n", GetSimpleManyToManyTableLazyLoadColsInitializations(entity, allEntities, customDTOClasses))}}

            if(this.modelId > 0){
                forkJoin({
                    mainUIFormDTO: this.apiService.get{{entity.Name}}MainUIFormDTO(this.modelId),
                })
                .subscribe(({ mainUIFormDTO }) => {
                    this.init{{entity.Name}}FormGroup(new {{entity.Name}}(mainUIFormDTO.{{entity.Name.FirstCharToLower()}}DTO));
{{string.Join("\n", GetOrderedOneToManyInitFormGroupForExistingObject(entity))}}
{{string.Join("\n", GetManyToManyMultiSelectInitFormControls(entity))}}
{{string.Join("\n", GetManyToManyMultiAutocompleteInitFormControls(entity))}}
                    this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                    this.loading = false;
                });
            }
            else{
                this.init{{entity.Name}}FormGroup(new {{entity.Name}}({id: 0}));
{{string.Join("\n", GetOrderedOneToManyInitFormGroupForNonExistingObject(entity))}}
                this.authorizationForSaveSubscription = this.handleAuthorizationForSave().subscribe();
                this.loading = false;
            }
        });
    }

    init{{entity.Name}}FormGroup({{entity.Name.FirstCharToLower()}}: {{entity.Name}}) {
        this.baseFormService.addFormGroup<{{entity.Name}}>(
            this.{{entity.Name.FirstCharToLower()}}FormGroup, 
            this.formGroup, 
            {{entity.Name.FirstCharToLower()}}, 
            this.{{entity.Name.FirstCharToLower()}}SaveBodyName,
            [{{string.Join(", ", GetCustomOnChangeProperties(entity))}}]
        );
        this.{{entity.Name.FirstCharToLower()}}FormGroup.mainDTOName = this.{{entity.Name.FirstCharToLower()}}SaveBodyName;

        this.onAfterFormGroupInit.next();
    }

    handleAuthorizationForSave = () => {
        return combineLatest([this.authService.currentUserPermissionCodes$, this.authorizedForSaveObservable()]).pipe(
            map(([currentUserPermissionCodes, isAuthorizedForSave]) => {
                if (currentUserPermissionCodes != null && isAuthorizedForSave != null) {
                    this.isAuthorizedForSave =
{{GetAdditionalPermissionCodes(entity)}}
                        (currentUserPermissionCodes.includes('Insert{{entity.Name}}') && this.modelId <= 0) || 
                        (currentUserPermissionCodes.includes('Update{{entity.Name}}') && this.modelId > 0) ||
                        isAuthorizedForSave;

                    if (this.isAuthorizedForSave) { 
{{GetControlsForNonAuthorizedUser(entity, allEntities, customDTOClasses, disable: false)}}
                    }
                    else{
{{GetControlsForNonAuthorizedUser(entity, allEntities, customDTOClasses, disable: true)}}
                    }

                    this.onIsAuthorizedForSaveChange.next(new IsAuthorizedForSaveEvent({
                        isAuthorizedForSave: this.isAuthorizedForSave, 
                        currentUserPermissionCodes: currentUserPermissionCodes
                    })); 
                }
            })
        );
    }

{{string.Join("\n", GetOrderedOneToManyInitFormArrayMethods(entity, allEntities))}}

{{string.Join("\n", GetOrderedOneToManyAddNewItemMethods(entity, allEntities))}}

{{string.Join("\n", GetSimpleManyToManyMethods(entity, allEntities))}}

{{string.Join("\n", GetAutocompleteSearchMethods(entity.Properties, entity, allEntities))}}

{{string.Join("\n", GetUploadImageMethods(entity.Properties, entity, allEntities))}}

    control(formControlName: string, formGroup: SpiderlyFormGroup){
        return getControl(formControlName, formGroup);
    }

    getFormArrayGroups<T>(formArray: SpiderlyFormArray): SpiderlyFormGroup<T>[]{
        return this.baseFormService.getFormArrayGroups<T>(formArray);
    }

    save(){
        this.onSave.next();
    }

	ngOnDestroy(){
        if (this.authorizationForSaveSubscription) {
            this.authorizationForSaveSubscription.unsubscribe();
        }
    }

}
""");
            }

            return result;
        }

        private static string GetShowFormBlocksVariables(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            StringBuilder sb = new();

            List<AngularFormBlock> formBlocks = GetAngularFormBlocks(entity, entities, customDTOClasses);

            foreach (AngularFormBlock formBlock in formBlocks)
            {
                sb.AppendLine($$"""
    @Input() show{{formBlock.Property.Name}}For{{formBlock.Property.EntityName}} = true;
""");
            }

            return sb.ToString();
        }

        private static string GetHelperVariables(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            StringBuilder sb = new();

            List<SpiderlyProperty> properties = GetAllFormControlProperties(entity.Properties.ToList(), entity, customDTOClasses, entities);

            foreach (SpiderlyProperty property in properties)
            {
                UIControlTypeCodes controlType = GetUIControlType(property);

                if (controlType == UIControlTypeCodes.Dropdown)
                {               
                sb.AppendLine($$"""
    @Output() on{{property.Name}}For{{property.EntityName}}Change = new EventEmitter<DropdownChangeEvent>();
""");
                }
                else if (controlType == UIControlTypeCodes.Calendar)
                {
                    sb.AppendLine($$"""
    @Input() showTimeOn{{property.Name}}For{{property.EntityName}} = false;
""");
                }
                else if (controlType == UIControlTypeCodes.CheckBox)
                {
                    sb.AppendLine($$"""
    @Output() on{{property.Name}}For{{property.EntityName}}Change = new EventEmitter<CheckboxChangeEvent>();
""");
                }
                else if (controlType == UIControlTypeCodes.ColorPicker)
                {
                    sb.AppendLine($$"""
    @Input() show{{property.Name}}TextFieldFor{{property.EntityName}} = true;
""");
                }
            }

            return sb.ToString();
        }



        private static string GetAdditionalPermissionCodes(SpiderlyClass entity)
        {
            StringBuilder sb = new();

            foreach (SpiderlyAttribute attribute in entity.Attributes)
            {
                if (attribute.Name == "UIAdditionalPermissionCodeForInsert")
                {
                    sb.AppendLine($$"""
                        (currentUserPermissionCodes.includes('{{attribute.Value}}') && this.modelId <= 0) || 
""");
                }
                else if (attribute.Name == "UIAdditionalPermissionCodeForUpdate")
                {
                    sb.AppendLine($$"""
                        (currentUserPermissionCodes.includes('{{attribute.Value}}') && this.modelId > 0) || 
""");
                }
            }

            return sb.ToString();
        }

        private static string GetControlsForNonAuthorizedUser(SpiderlyClass entity, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses, bool disable)
        {
            StringBuilder sb = new();

            List<AngularFormBlock> formBlocks = GetAngularFormBlocks(entity, allEntities, customDTOClasses);

            foreach (AngularFormBlock formBlock in formBlocks)
            {
                if (formBlock.FormControlName != null &&
                   (formBlock.Property.IsMultiSelectControlType() || formBlock.Property.IsMultiAutocompleteControlType())
                )
                {
                    sb.AppendLine($$"""
                        this.{{formBlock.FormControlName}}.{{(disable ? "disable" : "enable")}}();
""");
                }
                else if (formBlock.FormControlName == null &&
                         formBlock.Property.Type.IsOneToManyType() &&
                         formBlock.Property.HasUIOrderedOneToManyAttribute()
                )
                {
                    sb.AppendLine($$"""
                        this.baseFormService.{{(disable ? "disable" : "enable")}}AllFormControls(this.{{formBlock.Property.Name.FirstCharToLower()}}FormArray);
""");
                }
                else if (formBlock.FormControlName != null)
                {
                    sb.AppendLine($$"""
                        this.{{entity.Name.FirstCharToLower()}}FormGroup.controls.{{formBlock.FormControlName}}.{{(disable ? "disable" : "enable")}}();
""");
                }
            }

            return sb.ToString();
        }

        private static List<string> GetSimpleManyToManyMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                result.Add($$"""
    selected{{property.Name}}LazyLoadMethodFor{{entity.Name}} = (event: Filter): Observable<LazyLoadSelectedIdsResult> => {
        let filter: Filter = event;
        filter.additionalFilterIdLong = this.modelId;

        return this.apiService.lazyLoadSelected{{property.Name}}IdsFor{{entity.Name}}(filter);
    }
    areAll{{property.Name}}SelectedChangeFor{{entity.Name}}(event: AllClickEvent){
        this.areAll{{property.Name}}SelectedFor{{entity.Name}} = event.checked;
    }
    on{{property.Name}}LazyLoadFor{{entity.Name}}(event: Filter){
        this.last{{property.Name}}LazyLoadTableFilterFor{{entity.Name}} = event;
    }
""");
            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadColsInitializations(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                result.Add($$"""
            this.{{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}} = [
{{string.Join(",\n", GetSimpleManyToManyTableLazyLoadCols(property, entity, entities, customDTOClasses))}}
            ];
""");
            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadCols(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (UITableColumn col in property.GetUITableColumns())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                SpiderlyProperty extractedEntityProperty = extractedEntity?.Properties?.Where(x => x.Name == col.Field.Replace("DisplayName", "").Replace("CommaSeparated", ""))?.SingleOrDefault();

                SpiderlyClass extractedDTO = customDTOClasses.Where(x => x.Name == $"{Helpers.ExtractTypeFromGenericType(property.Type)}DTO").SingleOrDefault();
                SpiderlyProperty extractedDTOProperty = extractedDTO?.Properties?.Where(x => x.Name == col.Field)?.SingleOrDefault();

                result.Add($$"""
                {name: this.translocoService.translate('{{col.TranslationKey}}'), filterType: '{{GetTableColFilterType(extractedEntityProperty ?? extractedDTOProperty)}}', field: '{{col.Field.FirstCharToLower()}}' {{GetTableColAdditionalProperties(extractedEntityProperty ?? extractedDTOProperty, extractedEntity)}} }
""");
            }

            return result;
        }

        private static string GetTableColAdditionalProperties(SpiderlyProperty property, SpiderlyClass entity)
        {
            if (property.IsDropdownControlType())
                return $", filterField: '{property.Name.FirstCharToLower()}Id', dropdownOrMultiselectValues: await firstValueFrom(getPrimengDropdownNamebookOptions(this.apiService.get{property.Name}DropdownListFor{entity.Name}))";

            if (property.HasGenerateCommaSeparatedDisplayNameAttribute())
                return $", dropdownOrMultiselectValues: await firstValueFrom(getPrimengDropdownNamebookOptions(this.apiService.get{property.Name}DropdownListFor{entity.Name}))";

            switch (property.Type)
            {
                case "DateTime":
                case "DateTime?":
                    return ", showMatchModes: true";
                case "decimal":
                case "decimal?":
                case "float":
                case "float?":
                case "double":
                case "double?":
                case "long":
                case "long?":
                case "int":
                case "int?":
                case "byte":
                case "byte?":
                    return ", showMatchModes: true";
                default:
                    break;
            }

            return null;
        }

        private static string GetTableColFilterType(SpiderlyProperty property)
        {
            if (property.IsDropdownControlType())
                return "multiselect";

            if (property.HasGenerateCommaSeparatedDisplayNameAttribute())
                return "multiselect";

            if (property.Type.IsManyToOneType())
                return "text";

            switch (property.Type)
            {
                case "string":
                    return "text";
                case "bool":
                case "bool?":
                    return "boolean";
                case "DateTime":
                case "DateTime?":
                    return "date";
                case "decimal":
                case "decimal?":
                case "float":
                case "float?":
                case "double":
                case "double?":
                case "long":
                case "long?":
                case "int":
                case "int?":
                case "byte":
                case "byte?":
                    return "numeric";
                default:
                    break;
            }

            return null;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadSaveBodyAssignements(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                result.Add($$"""
            saveBody.selected{{property.Name}}Ids = this.newlySelected{{property.Name}}IdsFor{{entity.Name}};
            saveBody.unselected{{property.Name}}Ids = this.unselected{{property.Name}}IdsFor{{entity.Name}};
            saveBody.areAll{{property.Name}}Selected = this.areAll{{property.Name}}SelectedFor{{entity.Name}};
            saveBody.{{property.Name.FirstCharToLower()}}TableFilter = this.last{{property.Name}}LazyLoadTableFilterFor{{entity.Name}};
""");
            }

            return result;
        }

        private static List<string> GetSimpleManyToManyTableLazyLoadVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
    {{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}}: Column<{{extractedEntity.Name}}>[];
    getPaginated{{property.Name}}ListObservableMethodFor{{entity.Name}} = this.apiService.getPaginated{{property.Name}}ListFor{{entity.Name}};
    export{{property.Name}}ListToExcelObservableMethodFor{{entity.Name}} = this.apiService.export{{property.Name}}ListToExcelFor{{entity.Name}};
    newlySelected{{property.Name}}IdsFor{{entity.Name}}: number[] = [];
    unselected{{property.Name}}IdsFor{{entity.Name}}: number[] = [];
    areAll{{property.Name}}SelectedFor{{entity.Name}}: boolean = null;
    last{{property.Name}}LazyLoadTableFilterFor{{entity.Name}}: Filter;
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiSelectSaveBodyAssignements(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsMultiSelectControlType()))
            {
                result.Add($$"""
            saveBody.selected{{property.Name}}Ids = this.selected{{property.Name}}For{{entity.Name}}.getRawValue();
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiAutocompleteSaveBodyAssignements(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsMultiAutocompleteControlType()))
            {
                result.Add($$"""
            saveBody.selected{{property.Name}}Ids = this.selected{{property.Name}}For{{entity.Name}}.getRawValue()?.map(n => n.code);
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiSelectInitFormControls(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsMultiSelectControlType()))
            {
                result.Add($$"""
                    this.selected{{property.Name}}For{{entity.Name}}.setValue(
                        mainUIFormDTO.{{property.Name.FirstCharToLower()}}NamebookDTOList.map(n => { return n.id })
                    );
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiAutocompleteInitFormControls(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsMultiAutocompleteControlType()))
            {
                result.Add($$"""
                    this.selected{{property.Name}}For{{entity.Name}}.setValue(
                        mainUIFormDTO.{{property.Name.FirstCharToLower()}}NamebookDTOList.map(n => ({ label: n.displayName, code: n.id }))
                    );
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiSelectListForDropdownMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    (x.IsMultiSelectControlType() || x.IsDropdownControlType()) &&
                    x.HasUIDoNotGenerateAttribute() == false
                )
            )
            {
                result.Add($$"""
            getPrimengDropdownNamebookOptions(this.apiService.get{{property.Name}}DropdownListFor{{entity.Name}}, this.modelId).subscribe(po => {
                this.{{property.Name.FirstCharToLower()}}OptionsFor{{entity.Name}} = po;
            });
""");
            }

            return result;
        }

        private static List<string> GetManyToManyMultiControlTypesForkJoinParameters(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties
                .Where(x =>
                    x.IsMultiSelectControlType() ||
                    x.IsMultiAutocompleteControlType()))
            {
                result.Add($$"""
                    {{property.Name.FirstCharToLower()}}For{{entity.Name}}: this.apiService.get{{property.Name}}NamebookListFor{{entity.Name}}(this.modelId),
""");
            }

            return result;
        }

        private static List<string> GetCustomSpiderlyFormControls(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.IsMultiSelectControlType())
                {
                    result.Add($$"""
    selected{{property.Name}}For{{entity.Name}} = new SpiderlyFormControl<number[]>(null, {updateOn: 'change'});
""");
                }
                else if (property.IsMultiAutocompleteControlType())
                {
                    result.Add($$"""
    selected{{property.Name}}For{{entity.Name}} = new SpiderlyFormControl<PrimengOption[]>(null, {updateOn: 'change'});
""");
                }
            }

            return result;
        }

        #region Ordered One To Many

        private static List<string> GetOrderedOneToManyAddNewItemMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
    addNewItemTo{{property.Name}}(index: number){ 
        this.baseFormService.addNewFormGroupToFormArray(
            this.{{property.Name.FirstCharToLower()}}FormArray, 
            new {{extractedEntity.Name}}({id: 0}), 
            index
        );
    }
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyInitFormArrayMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
    init{{property.Name}}FormArray({{property.Name.FirstCharToLower()}}: {{extractedEntity.Name}}[]){
        this.{{property.Name.FirstCharToLower()}}FormArray = this.baseFormService.initFormArray(
            this.formGroup, 
            {{property.Name.FirstCharToLower()}}, 
            this.{{property.Name.FirstCharToLower()}}Model, 
            this.{{property.Name.FirstCharToLower()}}SaveBodyName, 
            this.{{property.Name.FirstCharToLower()}}TranslationKey, 
            true
        );
        this.{{property.Name.FirstCharToLower()}}CrudMenu = this.getCrudMenuForOrderedData(this.{{property.Name.FirstCharToLower()}}FormArray, new {{extractedEntity.Name}}({id: 0}), this.{{property.Name.FirstCharToLower()}}LastIndexClicked, false);
{{GetFormArrayEmptyValidator(property)}}
    }
""");
            }

            return result;
        }

        private static string GetFormArrayEmptyValidator(SpiderlyProperty property)
        {
            if (property.HasRequiredAttribute())
            {
                return $$"""
        this.{{property.Name.FirstCharToLower()}}FormArray.validator = this.validatorService.isFormArrayEmpty(this.{{property.Name.FirstCharToLower()}}FormArray);
""";
            }

            return null;
        }

        private static List<string> GetOrderedOneToManyForkJoinParameters(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                result.Add($$"""
                    {{property.Name.FirstCharToLower()}}For{{entity.Name}}: this.apiService.getOrdered{{property.Name}}For{{entity.Name}}(this.modelId),
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyInitFormGroupForExistingObject(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                result.Add($$"""
                    this.init{{property.Name}}FormArray(mainUIFormDTO.ordered{{property.Name}}DTO);
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyInitFormGroupForNonExistingObject(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                result.Add($$"""
                this.init{{property.Name}}FormArray([]);
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManySaveBodyAssignements(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
            saveBody.{{property.Name.FirstCharToLower()}}DTO = this.{{property.Name.FirstCharToLower()}}FormArray.getRawValue();
""");
            }

            return result;
        }

        private static List<string> GetOrderedOneToManyVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();

                result.Add($$"""
    {{property.Name.FirstCharToLower()}}Model = new {{extractedEntity.Name}}();
    {{property.Name.FirstCharToLower()}}SaveBodyName: string = nameof<{{entity.Name}}SaveBody>('{{property.Name.FirstCharToLower()}}DTO');
    {{property.Name.FirstCharToLower()}}TranslationKey: string = new {{extractedEntity.Name}}().typeName;
    {{property.Name.FirstCharToLower()}}FormArray: SpiderlyFormArray<{{extractedEntity.Name}}>;
    {{property.Name.FirstCharToLower()}}LastIndexClicked = new LastMenuIconIndexClicked();
    {{property.Name.FirstCharToLower()}}CrudMenu: MenuItem[] = [];
    @Input() {{property.Name.FirstCharToLower()}}PanelCollapsed: boolean = false;
""");
            }

            return result;
        }


        /// <summary>
        /// </summary>
        /// <param name="property">eg. List<SegmentationItem> SegmentationItems</param>
        /// <param name="allEntities"></param>
        /// <param name="customDTOClasses"></param>
        /// <returns></returns>
        private static string GetOrderedOneToManyBlock(SpiderlyProperty property, List<SpiderlyClass> allEntities, List<SpiderlyClass> customDTOClasses)
        {
            SpiderlyClass extractedEntity = allEntities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault(); // eg. SegmentationItem

            // Every property of SegmentationItem without the many to one reference (Segmentation) and enumerable properties
            List<SpiderlyProperty> propertyBlocks = extractedEntity.Properties
                .Where(x =>
                    x.WithMany() != property.Name &&
                    x.Type.IsEnumerable() == false
                )
                .ToList();

            return $$"""
                     <div *ngIf="show{{property.Name}}For{{property.EntityName}}" class="col-12">
                        <spiderly-panel [toggleable]="true" [collapsed]="{{property.Name.FirstCharToLower()}}PanelCollapsed">
                            <panel-header [title]="t('{{property.Name}}')" icon="pi pi-list"></panel-header>
                            <panel-body [normalBottomPadding]="true">
                                @for ({{extractedEntity.Name.FirstCharToLower()}}FormGroup of getFormArrayGroups({{property.Name.FirstCharToLower()}}FormArray); track {{extractedEntity.Name.FirstCharToLower()}}FormGroup; let index = $index; let last = $last) {
                                    <index-card 
                                    [index]="index" 
                                    [last]="false" 
                                    [crudMenu]="{{property.Name.FirstCharToLower()}}CrudMenu" 
                                    [showCrudMenu]="isAuthorizedForSave"
                                    (onMenuIconClick)="{{property.Name.FirstCharToLower()}}LastIndexClicked.index = $event"
                                    >
                                        <form [formGroup]="{{extractedEntity.Name.FirstCharToLower()}}FormGroup" class="grid">
{{string.Join("\n", GetPropertyBlocks(propertyBlocks, extractedEntity, allEntities, customDTOClasses, isFromOrderedOneToMany: true))}}
                                        </form>
                                    </index-card>
                                }

                                <div class="panel-add-button">
                                    <spiderly-button [disabled]="!isAuthorizedForSave" (onClick)="addNewItemTo{{property.Name}}(null)" [label]="t('AddNew{{Helpers.ExtractTypeFromGenericType(property.Type)}}')" icon="pi pi-plus"></spiderly-button>
                                </div>

                            </panel-body>
                        </spiderly-panel>
                    </div>
""";
        }

        #endregion

        private static List<string> GetCustomOnChangeProperties(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.IsColorControlType() || property.Type == "DateTime")
                {
                    result.Add($"'{property.Name.FirstCharToLower()}'");
                }
            }

            return result;
        }

        private static List<string> GetPrimengOptionVariables(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in properties.Where(x => x.Attributes.Any(x => x.Name == "UIDoNotGenerate") == false))
            {
                if (property.Attributes.Any(x => x.Name == "UIOrderedOneToMany"))
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                    List<SpiderlyProperty> extractedProperties = extractedEntity.Properties
                        .Where(x =>
                            x.WithMany() != property.Name &&
                            x.Type.IsEnumerable() == false
                        )
                        .ToList();

                    GetPrimengOptionVariables(extractedProperties, extractedEntity, entities);

                    continue;
                }

                UIControlTypeCodes controlType = GetUIControlType(property);

                if (controlType == UIControlTypeCodes.Autocomplete ||
                    controlType == UIControlTypeCodes.Dropdown ||
                    controlType == UIControlTypeCodes.MultiAutocomplete ||
                    controlType == UIControlTypeCodes.MultiSelect)
                {
                    result.Add($$"""
    {{property.Name.FirstCharToLower()}}OptionsFor{{entity.Name}}: PrimengOption[];
""");

                }
            }

            return result;
        }

        private static List<string> GetAutocompleteSearchMethods(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in properties.Where(x => x.Attributes.Any(x => x.Name == "UIDoNotGenerate") == false))
            {
                if (property.Attributes.Any(x => x.Name == "UIOrderedOneToMany"))
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                    List<SpiderlyProperty> extractedProperties = extractedEntity.Properties
                        .Where(x =>
                            x.WithMany() != property.Name &&
                            x.Type.IsEnumerable() == false
                        )
                        .ToList();

                    GetAutocompleteSearchMethods(extractedProperties, extractedEntity, entities);

                    continue;
                }

                UIControlTypeCodes controlType = GetUIControlType(property);

                if (controlType == UIControlTypeCodes.Autocomplete ||
                    controlType == UIControlTypeCodes.MultiAutocomplete)
                {
                    result.Add($$"""
    search{{property.Name}}For{{entity.Name}}(event: AutoCompleteCompleteEvent) {
        getPrimengAutocompleteNamebookOptions(this.apiService.get{{property.Name}}AutocompleteListFor{{entity.Name}}, 50, event?.query ?? '').subscribe(po => {
            this.{{property.Name.FirstCharToLower()}}OptionsFor{{entity.Name}} = po;
        });
    }
""");

                }

            }

            return result;
        }

        private static List<string> GetUploadImageMethods(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in properties.Where(x => x.HasUIDoNotGenerateAttribute() == false))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                    List<SpiderlyProperty> extractedProperties = extractedEntity.Properties
                        .Where(x =>
                            x.WithMany() != property.Name &&
                            x.Type.IsEnumerable() == false
                        )
                        .ToList();

                    GetUploadImageMethods(extractedProperties, extractedEntity, entities);

                    continue;
                }

                UIControlTypeCodes controlType = GetUIControlType(property);

                if (controlType == UIControlTypeCodes.File)
                {
                    result.Add($$"""
    upload{{property.Name}}For{{entity.Name}}(event: SpiderlyFileSelectEvent){
        this.apiService.upload{{property.Name}}For{{entity.Name}}(event.formData).subscribe((completeFileName: string) => {
            this.{{entity.Name.FirstCharToLower()}}FormGroup.controls.{{property.Name.FirstCharToLower()}}.setValue(completeFileName);
        });
    }
""");

                }

            }

            return result;
        }

        private static List<string> GetForkJoinParameterNames(SpiderlyClass entity)
        {
            List<string> result = new();

            result.Add(entity.Name.FirstCharToLower());

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (property.HasUIOrderedOneToManyAttribute() ||
                    property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add($"{property.Name.FirstCharToLower()}For{entity.Name}");
                }
            }

            return result;
        }

        private static List<string> GetPropertyBlocks(
            List<SpiderlyProperty> properties,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses,
            bool isFromOrderedOneToMany
        )
        {
            List<string> result = new();

            SpiderlyClass customDTOClass = customDTOClasses.Where(x => x.Name.Replace("DTO", "") == entity.Name).SingleOrDefault();

            if (customDTOClass != null)
                properties.AddRange(customDTOClass.Properties);

            foreach (SpiderlyProperty property in GetPropertiesForUIBlocks(properties))
            {
                if (property.Attributes.Any(x => x.Name == "UIOrderedOneToMany"))
                {
                    result.Add(GetOrderedOneToManyBlock(property, allEntities, customDTOClasses));

                    continue;
                }

                string controlType = GetUIStringControlType(GetUIControlType(property));

                result.Add($$"""
                    <div {{GetNgIfForPropertyBlock(property, isFromOrderedOneToMany)}} class="{{GetUIControlWidth(property)}}">
                        <{{controlType}} {{GetControlAttributes(property, entity)}}></{{controlType}}>
                    </div>
""");
            }

            return result;
        }

        private static List<AngularFormBlock> GetAngularFormBlocks(
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses
        )
        {
            List<AngularFormBlock> result = new();

            List<SpiderlyProperty> properties = entity.Properties.ToList();

            SpiderlyClass customDTOClass = customDTOClasses.Where(x => x.Name.Replace("DTO", "") == entity.Name).SingleOrDefault();

            if (customDTOClass != null)
                properties.AddRange(customDTOClass.Properties);

            foreach (SpiderlyProperty property in GetPropertiesForUIBlocks(properties))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    result.Add(new AngularFormBlock // FT: Name is null because there is no form control for the one to many properties
                    {
                        Property = property
                    });

                    continue;
                }

                UIControlTypeCodes controlType = GetUIControlType(property);

                if (property.IsMultiSelectControlType() ||
                    property.IsMultiAutocompleteControlType())
                {
                    result.Add(new AngularFormBlock
                    {
                        FormControlName = $"selected{property.Name}For{entity.Name}",
                        Property = property,
                    });
                }
                else if (controlType != UIControlTypeCodes.Table)
                {
                    result.Add(new AngularFormBlock
                    {
                        FormControlName = GetFormControlName(property),
                        Property = property,
                    });
                }
                else if (controlType == UIControlTypeCodes.Table)
                {
                    result.Add(new AngularFormBlock // FT: Name is null because there is no form control for the table
                    {
                        Property = property,
                    });
                }
            }

            return result;
        }

        private static string GetControlHtmlAttributeValue(SpiderlyProperty property, SpiderlyClass entity)
        {
            if (property.IsMultiSelectControlType() ||
                property.IsMultiAutocompleteControlType())
            {
                return $"selected{property.Name}For{entity.Name}";
            }

            return $"control('{GetFormControlName(property)}', {entity.Name.FirstCharToLower()}FormGroup)";
        }

        private static List<SpiderlyProperty> GetPropertiesForUIBlocks(List<SpiderlyProperty> properties)
        {
            List<SpiderlyProperty> orderedProperties = properties
                .Where(x =>
                    x.Name != "Version" &&
                    x.Name != "Id" &&
                    x.Name != "CreatedAt" &&
                    x.Name != "ModifiedAt" &&
                    (
                        x.Type.IsEnumerable() == false ||
                        x.HasUIOrderedOneToManyAttribute() ||
                        x.IsMultiSelectControlType() ||
                        x.IsMultiAutocompleteControlType() ||
                        x.HasSimpleManyToManyTableLazyLoadAttribute()
                    ) &&
                    x.HasUIDoNotGenerateAttribute() == false
                )
                .OrderBy(x =>
                    x.Attributes.Any(attr => attr.Name == "BlobName") ? 0 :
                    x.Attributes.Any(attr => attr.Value == UIControlTypeCodes.TextArea.ToString()) ? 2 :
                    x.Attributes.Any(attr => attr.Value == UIControlTypeCodes.Editor.ToString()) ? 3 :
                    x.Attributes.Any(attr => attr.Name == "UIOrderedOneToMany") ? 4 :
                    x.Attributes.Any(attr => attr.Name == "SimpleManyToManyTableLazyLoad") ? 5
                    : 1)
                .ToList();

            return orderedProperties;
        }

        private static List<SpiderlyProperty> GetAllFormControlProperties(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> customDTOClasses, List<SpiderlyClass> entities)
        {
            List<SpiderlyProperty> result = new();

            SpiderlyClass customDTOClass = customDTOClasses.Where(x => x.Name.Replace("DTO", "") == entity.Name).SingleOrDefault();

            if (customDTOClass != null)
                properties.AddRange(customDTOClass.Properties);

            List<SpiderlyProperty> filteredProperties = properties
                .Where(x =>
                    x.Name != "Version" &&
                    x.Name != "Id" &&
                    x.Name != "CreatedAt" &&
                    x.Name != "ModifiedAt" &&
                    (
                        x.Type.IsEnumerable() == false ||
                        x.HasUIOrderedOneToManyAttribute() ||
                        x.IsMultiSelectControlType() ||
                        x.IsMultiAutocompleteControlType() ||
                        x.HasSimpleManyToManyTableLazyLoadAttribute()
                    ) &&
                    x.HasUIDoNotGenerateAttribute() == false
                )
                .ToList();

            foreach (SpiderlyProperty property in filteredProperties)
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = entities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).SingleOrDefault();
                    
                    List<SpiderlyProperty> extractedProperties = extractedEntity.Properties
                        .Where(x =>
                            x.WithMany() != property.Name &&
                            x.Type.IsEnumerable() == false
                        )
                        .ToList();

                    result.AddRange(GetAllFormControlProperties(extractedProperties, extractedEntity, customDTOClasses, entities));

                    continue;
                }

                result.Add(property);
            }

            return result;
        }

        private static string GetFormControlName(SpiderlyProperty property)
        {
            if (property.Type.IsManyToOneType())
                return $"{property.Name.FirstCharToLower()}Id";

            return property.Name.FirstCharToLower();
        }

        private static string GetControlAttributes(SpiderlyProperty property, SpiderlyClass entity)
        {
            UIControlTypeCodes controlType = GetUIControlType(property);

            if (controlType == UIControlTypeCodes.Decimal)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [decimal]=\"true\" [maxFractionDigits]=\"{property.GetDecimalScale()}\"";
            }
            else if (controlType == UIControlTypeCodes.Calendar)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [showTime]=\"showTimeOn{property.Name}For{entity.Name}\"";
            }
            else if (controlType == UIControlTypeCodes.CheckBox)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" (onChange)=\"on{property.Name}For{entity.Name}Change.next($event)\"";
            }
            else if (controlType == UIControlTypeCodes.File)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [fileData]=\"{entity.Name.FirstCharToLower()}FormGroup.controls.{property.Name.FirstCharToLower()}Data.getRawValue()\" [objectId]=\"{entity.Name.FirstCharToLower()}FormGroup.controls.id.getRawValue()\" (onFileSelected)=\"upload{property.Name}For{entity.Name}($event)\" [disabled]=\"!isAuthorizedForSave\"";
            }
            else if (controlType == UIControlTypeCodes.Dropdown)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" (onChange)=\"on{property.Name}For{entity.Name}Change.next($event)\"";
            }
            else if (controlType == UIControlTypeCodes.Autocomplete)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" [displayName]=\"{entity.Name.FirstCharToLower()}FormGroup.controls.{property.Name.FirstCharToLower()}DisplayName.getRawValue()\" (onTextInput)=\"search{property.Name}For{entity.Name}($event)\"";
            }
            else if (controlType == UIControlTypeCodes.MultiSelect)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" [label]=\"t('{property.Name}')\"";
            }
            else if (controlType == UIControlTypeCodes.MultiAutocomplete)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" (onTextInput)=\"search{property.Name}For{entity.Name}($event)\" [label]=\"t('{property.Name}')\"";
            }
            else if (controlType == UIControlTypeCodes.Table)
            {
                return $$"""

                            [tableTitle]="t('{{property.Name}}')" 
                            [cols]="{{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}}" 
                            [getPaginatedListObservableMethod]="getPaginated{{property.Name}}ListObservableMethodFor{{entity.Name}}" 
                            [exportListToExcelObservableMethod]="export{{property.Name}}ListToExcelObservableMethodFor{{entity.Name}}"
                            [showAddButton]="false" 
                            [readonly]="!isAuthorizedForSave"
                            selectionMode="multiple"
                            [newlySelectedItems]="newlySelected{{property.Name}}IdsFor{{entity.Name}}" 
                            [unselectedItems]="unselected{{property.Name}}IdsFor{{entity.Name}}" 
                            [rows]="5" 
                            (onLazyLoad)="on{{property.Name}}LazyLoadFor{{entity.Name}}($event)"
                            [selectedLazyLoadObservableMethod]="selected{{property.Name}}LazyLoadMethodFor{{entity.Name}}" 
                            (onIsAllSelectedChange)="areAll{{property.Name}}SelectedChangeFor{{entity.Name}}($event)"
""";
            }
            else if (controlType == UIControlTypeCodes.ColorPicker)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\" [showInputTextField]=\"show{property.Name}TextFieldFor{entity.Name}\"";
            }

            return $"[control]=\"{GetControlHtmlAttributeValue(property, entity)}\"";
        }

        private static string GetUIControlWidth(SpiderlyProperty property)
        {
            SpiderlyAttribute uiControlWidthAttribute = property.Attributes.Where(x => x.Name == "UIControlWidth").SingleOrDefault();

            if (uiControlWidthAttribute != null)
                return uiControlWidthAttribute.Value;

            UIControlTypeCodes controlType = GetUIControlType(property);

            if (controlType == UIControlTypeCodes.File ||
                controlType == UIControlTypeCodes.TextArea ||
                controlType == UIControlTypeCodes.MultiSelect ||
                controlType == UIControlTypeCodes.MultiAutocomplete ||
                controlType == UIControlTypeCodes.Table ||
                controlType == UIControlTypeCodes.Editor)
            {
                return "col-12";
            }

            return "col-12 md:col-6";
        }

        private static UIControlTypeCodes GetUIControlType(SpiderlyProperty property)
        {
            SpiderlyAttribute uiControlTypeAttribute = property.Attributes.Where(x => x.Name == "UIControlType").SingleOrDefault();

            if (uiControlTypeAttribute != null)
            {
                Enum.TryParse(uiControlTypeAttribute.Value, out UIControlTypeCodes parseResult);
                return parseResult;
            }

            if (property.IsBlob())
                return UIControlTypeCodes.File;

            if (property.Type.IsManyToOneType())
                return UIControlTypeCodes.Autocomplete;

            if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                return UIControlTypeCodes.Table;

            switch (property.Type)
            {
                case "string":
                    return UIControlTypeCodes.TextBox;
                case "bool":
                case "bool?":
                    return UIControlTypeCodes.CheckBox;
                case "DateTime":
                case "DateTime?":
                    return UIControlTypeCodes.Calendar;
                case "decimal":
                case "decimal?":
                case "float":
                case "float?":
                case "double":
                case "double?":
                    return UIControlTypeCodes.Decimal;
                case "long":
                case "long?":
                case "int":
                case "int?":
                case "byte":
                case "byte?":
                    return UIControlTypeCodes.Integer;
                default:
                    break;
            }

            return UIControlTypeCodes.None; // Note: We can't throw exception here
        }

        private static string GetUIStringControlType(UIControlTypeCodes controlType)
        {
            switch (controlType)
            {
                case UIControlTypeCodes.Autocomplete:
                    return "spiderly-autocomplete";
                case UIControlTypeCodes.Calendar:
                    return "spiderly-calendar";
                case UIControlTypeCodes.CheckBox:
                    return "spiderly-checkbox";
                case UIControlTypeCodes.ColorPicker:
                    return "spiderly-colorpicker";
                case UIControlTypeCodes.Dropdown:
                    return "spiderly-dropdown";
                case UIControlTypeCodes.Editor:
                    return "spiderly-editor";
                case UIControlTypeCodes.File:
                    return "spiderly-file";
                case UIControlTypeCodes.MultiAutocomplete:
                    return "spiderly-multiautocomplete";
                case UIControlTypeCodes.MultiSelect:
                    return "spiderly-multiselect";
                case UIControlTypeCodes.Integer:
                case UIControlTypeCodes.Decimal:
                    return "spiderly-number";
                case UIControlTypeCodes.Password:
                    return "spiderly-password";
                case UIControlTypeCodes.TextArea:
                    return "spiderly-textarea";
                case UIControlTypeCodes.TextBlock:
                    return "spiderly-textblock";
                case UIControlTypeCodes.TextBox:
                    return "spiderly-textbox";
                case UIControlTypeCodes.Table:
                    return "spiderly-data-table";
                default:
                    return $"Unknown UIControlType: '{controlType}'.";

            }
        }

        private static string GetNgIfForPropertyBlock(SpiderlyProperty property, bool isFromOrderedOneToMany)
        {
            if (isFromOrderedOneToMany)
                return null;

            return $$"""
*ngIf="show{{property.Name}}For{{property.EntityName}}"
""";
        }

        private static string GetImports(List<SpiderlyClass> customDTOClasses, List<SpiderlyClass> entities)
        {
            List<AngularImport> customDTOImports = customDTOClasses
                .Select(x => new AngularImport
                {
                    Namespace = x.Namespace.Replace(".DTO", ""),
                    Name = x.Name.Replace("DTO", "")
                })
                .ToList();

            List<AngularImport> entityImports = entities
                .Select(x => new AngularImport
                {
                    Namespace = x.Namespace.Replace(".Entities", ""),
                    Name = x.Name
                })
                .ToList();

            List<AngularImport> saveBodyImports = entities
                .Select(x => new AngularImport
                {
                    Namespace = x.Namespace.Replace(".Entities", ""),
                    Name = $"{x.Name}SaveBody"
                })
                .ToList();

            List<AngularImport> imports = customDTOImports.Concat(entityImports).Concat(saveBodyImports).ToList();

            return $$"""
import { ValidatorService } from 'src/app/business/services/validators/validators';
import { DropdownChangeEvent } from 'primeng/dropdown';
import { CheckboxChangeEvent } from 'primeng/checkbox';
import { TranslateLabelsService } from '../../services/translates/merge-labels';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ApiService } from '../../services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { ActivatedRoute } from '@angular/router';
import { combineLatest, firstValueFrom, forkJoin, map, Observable, of, Subscription } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../../services/auth/auth.service';
import { SpiderlyControlsModule, CardSkeletonComponent, IndexCardComponent, IsAuthorizedForSaveEvent, SpiderlyDataTableComponent, SpiderlyFormArray, BaseEntity, LastMenuIconIndexClicked, SpiderlyFormGroup, SpiderlyButton, nameof, BaseFormService, getControl, Column, Filter, LazyLoadSelectedIdsResult, AllClickEvent, SpiderlyFileSelectEvent, getPrimengDropdownNamebookOptions, PrimengOption, SpiderlyFormControl, getPrimengAutocompleteNamebookOptions, SpiderlyPanelsModule } from 'spiderly';
{{string.Join("\n", GetDynamicNgImports(imports))}}
""";
        }

        /// <summary>
        /// Key - Namespace
        /// Value - Name of the class to import in Angular
        /// </summary>
        private static List<string> GetDynamicNgImports(List<AngularImport> imports)
        {
            List<string> result = new();

            foreach (var projectImports in imports.GroupBy(x => x.Namespace))
            {
                string projectName = projectImports.Key.Split('.').Last(); // eg. Security

                if (projectName == "Shared" ||
                    projectName == "Security")
                    continue;

                result.Add($$"""
import { {{string.Join(", ", projectImports.DistinctBy(x => x.Name).Select(x => x.Name))}} } from '../../entities/{{projectName.FromPascalToKebabCase()}}-entities.generated';
""");
            }

            return result;
        }
    }
}
