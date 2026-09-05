using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Angular
{
    internal static class NgDetailsDataGenerator
    {
        internal static string GetEntityInitBlock(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            var (forkJoinParams, formGroupInitValues, newEntityInitValues) = CollectComplexManyToManyListInfo(entity, allEntities, isFromOrderedOneToMany: false);

            string existingEntityBlock = GetExistingEntityBlock(entity, forkJoinParams, formGroupInitValues);
            string newEntityBlock = GetNewEntityBlock(entity, forkJoinParams, formGroupInitValues, newEntityInitValues);

            return $$"""
            if (this.modelId > 0) {
{{existingEntityBlock}}
            }
            else {
{{newEntityBlock}}
            }
""";
        }

        internal static List<string> GetSimpleManyToManyMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsIncludedInDetailsUi(entity) && x.HasSimpleManyToManyTableLazyLoadAttribute()))
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

        internal static List<string> GetManyToManyTableColsInitializations(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsIncludedInDetailsUi(entity)))
            {
                if (
                    property.HasSimpleManyToManyTableLazyLoadAttribute() ||
                    property.HasComplexManyToManyReadonlyTableAttribute()
                )
                {
                    result.Add($$"""
            this.{{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}} = [
{{string.Join(",\n", GetSimpleManyToManyTableLazyLoadCols(property, entity, entities, customDTOClasses))}}
            ];
""");
                }
            }

            return result;
        }

        internal static List<string> GetManyToManyMultiSelectListForDropdownMethods(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.IsIncludedInDetailsUi(entity)))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    result.AddRange(GetManyToManyMultiSelectListForDropdownMethods(extractedEntity, entities));

                    continue;
                }

                if (property.IsMultiSelectControlType() == false && property.IsDropdownControlType() == false)
                    continue;

                result.Add($$"""
            this.apiService.get{{property.Name}}DropdownListFor{{entity.Name}}().subscribe(no => {
                this.{{property.Name.FirstCharToLower()}}OptionsFor{{entity.Name}} = no;
            });
""");
            }

            return result;
        }

        /// <summary>
        /// Emits the client-side option population for enum dropdown properties (e.g.
        /// <c>this.severityOptionsForAnnouncement = getAnnouncementSeverityCodesNamebookList(this.translocoService);</c>).
        /// Enums render as a dropdown via <see cref="NgDetailsPropertyBlockGenerator.GetUIControlType"/> and get an
        /// <c>optionsFor{Entity}</c> variable, but — unlike FK dropdowns — they have no backend list endpoint; their
        /// options come from the generated TS enum, so they need their own population path (FK populate gates on the
        /// explicit <c>[UIControlType("Dropdown")]</c> attribute, which an enum never carries).
        /// </summary>
        internal static List<string> GetEnumDropdownOptionsInitializations(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            // No explicit ordered-O2M recursion here (unlike the FK populate): GetEnumDropdownContexts already
            // flattens nested properties, and an enum's option list is scope-independent (the same global list
            // regardless of which nested form group binds it), so there's nothing per-scope to recurse for.
            foreach (PropertyWithContext context in NgDetailsPropertyBlockGenerator.GetEnumDropdownContexts(entity, entities, customDTOClasses))
            {
                result.Add($$"""
            this.{{context.Property.Name.FirstCharToLower()}}OptionsFor{{context.Entity.Name}} = get{{context.Property.Type.CoreName}}NamebookList(this.translocoService);
""");
            }

            return result;
        }

        internal static List<string> GetAutocompleteSearchMethods(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            List<PropertyWithContext> contexts = NgDetailsPropertyBlockGenerator.GetAllPropertiesWithContext(entity, entities, customDTOClasses);

            foreach (PropertyWithContext context in contexts.Where(x => x.FormControlName != null))
            {
                UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(context.Property);

                if (controlType == UIControlTypeCodes.Autocomplete ||
                    controlType == UIControlTypeCodes.MultiAutocomplete)
                {
                    result.Add($$"""
    search{{context.Property.Name}}For{{context.Entity.Name}}(event: AutoCompleteCompleteEvent) {
        this.apiService.get{{context.Property.Name}}AutocompleteListFor{{context.Entity.Name}}(50, event?.query ?? '').subscribe(no => {
            this.{{context.Property.Name.FirstCharToLower()}}OptionsFor{{context.Entity.Name}} = no;
        });
    }
""");
                }
            }

            return result;
        }

        internal static List<string> GetBlobUploadedOutputVariables(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> entities, bool isFromOrderedOneToMany)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in properties.Where(x => x.IsIncludedInDetailsUi(entity)))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    result.AddRange(GetBlobUploadedOutputVariables(extractedEntity.Properties, extractedEntity, entities, isFromOrderedOneToMany: true));

                    continue;
                }

                UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(property);

                if (controlType == UIControlTypeCodes.File)
                {
                    if (isFromOrderedOneToMany)
                    {
                        result.Add($$"""
    @Output() on{{property.Name}}For{{entity.Name}}Uploaded = new EventEmitter<{ event: SpiderlyFileSelectEvent; formGroup: SpiderlyFormGroup }>();
""");
                    }
                    else
                    {
                        result.Add($$"""
    @Output() on{{property.Name}}Uploaded = new EventEmitter<SpiderlyFileSelectEvent>();
""");
                    }
                }
            }

            return result;
        }

        internal static List<string> GetUploadImageMethods(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> entities, bool isFromOrderedOneToMany)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in properties.Where(x => x.IsIncludedInDetailsUi(entity)))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    result.AddRange(GetUploadImageMethods(extractedEntity.Properties, extractedEntity, entities, isFromOrderedOneToMany: true));

                    continue;
                }

                UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(property);

                if (controlType == UIControlTypeCodes.File)
                {
                    if (isFromOrderedOneToMany)
                    {
                        result.Add($$"""
    upload{{property.Name}}For{{entity.Name}}(event: SpiderlyFileSelectEvent, formGroup: SpiderlyFormGroup){
        this.apiService.upload{{property.Name}}For{{entity.Name}}(event.formData).subscribe((completeFileName: string) => {
            formGroup.controls['{{property.Name.FirstCharToLower()}}'].setValue(completeFileName);
            this.on{{property.Name}}For{{entity.Name}}Uploaded.emit({ event, formGroup });
        });
    }
""");
                    }
                    else
                    {
                        result.Add($$"""
    upload{{property.Name}}For{{entity.Name}}(event: SpiderlyFileSelectEvent, formGroup: SpiderlyFormGroup){
        this.apiService.upload{{property.Name}}For{{entity.Name}}(event.formData).subscribe((completeFileName: string) => {
            formGroup.controls['{{property.Name.FirstCharToLower()}}'].setValue(completeFileName);
            this.on{{property.Name}}Uploaded.emit(event);
        });
    }
""");
                    }
                }
                else if ((controlType == UIControlTypeCodes.Editor || controlType == UIControlTypeCodes.Markdown) && property.HasS3PublicStorageAttribute())
                {
                    result.Add($$"""
    upload{{property.Name}}ImageFor{{entity.Name}} = (formData: FormData): Observable<EditorImageUploadResult> => {
        return this.apiService.upload{{property.Name}}ImageFor{{entity.Name}}(formData);
    }
""");
                }
            }

            return result;
        }

        private static string GetExistingEntityBlock(SpiderlyClass entity, List<string> forkJoinParams, List<string> formGroupInitValues)
        {
            string forkJoinParamsBlock = forkJoinParams.Count > 0 ? "\n" + string.Join("\n", forkJoinParams).TrimEnd('\r', '\n') : "";

            string allForkJoinParams = $"                    mainUIFormDTO: this.apiService.get{entity.Name}MainUIFormDTO(this.modelId),{forkJoinParamsBlock}";

            string initFormGroupStatement = $$"""
                    const saveBody = this.baseFormService.mapMainUIFormToSaveBody(
                        {{entity.Name}}MainUIForm,
                        data.mainUIFormDTO,
                    );
                    this.baseFormService.initFormGroup(this.parentFormGroup, {{entity.Name}}SaveBody, saveBody);
""";

            return BuildForkJoinSubscribeBlock(allForkJoinParams, initFormGroupStatement, BuildFormGroupInitValuesBlock(formGroupInitValues));
        }

        private static string GetNewEntityBlock(SpiderlyClass entity, List<string> forkJoinParams, List<string> formGroupInitValues, List<string> newEntityInitValues)
        {
            if (forkJoinParams.Count == 0)
            {
                return $$"""
                this.baseFormService.initFormGroup(this.parentFormGroup, {{entity.Name}}SaveBody);
                await this.handleAuthorizationForSave();
                this.loading = false;
                this.onAfterFormGroupInit.next();
""";
            }

            string newEntityInitFormGroupArg = "";
            if (newEntityInitValues.Count > 0)
            {
                newEntityInitFormGroupArg = $$"""
, {
{{string.Join(",\n", newEntityInitValues)}}
                    }
""";
                newEntityInitFormGroupArg = newEntityInitFormGroupArg.TrimEnd('\r', '\n');
            }

            string allForkJoinParams = string.Join("\n", forkJoinParams).TrimEnd('\r', '\n');

            string initFormGroupStatement = $"                    this.baseFormService.initFormGroup(this.parentFormGroup, {entity.Name}SaveBody{newEntityInitFormGroupArg});";

            return BuildForkJoinSubscribeBlock(allForkJoinParams, initFormGroupStatement, BuildFormGroupInitValuesBlock(formGroupInitValues));
        }

        private static string BuildFormGroupInitValuesBlock(List<string> formGroupInitValues) =>
            formGroupInitValues.Count > 0 ? "\n" + string.Join("\n", formGroupInitValues) : "";

        private static string BuildForkJoinSubscribeBlock(string forkJoinParams, string initFormGroupStatement, string formGroupInitValuesBlock) =>
            $$"""
                forkJoin({
{{forkJoinParams}}
                })
                .subscribe(async (data) => {
{{initFormGroupStatement}}{{formGroupInitValuesBlock}}
                    await this.handleAuthorizationForSave();
                    this.loading = false;
                    this.onAfterFormGroupInit.next();
                });
""";

        private static (List<string> ForkJoinParams, List<string> FormGroupInitialValues, List<string> NewEntityInitValues) CollectComplexManyToManyListInfo(SpiderlyClass entity, List<SpiderlyClass> allEntities, bool isFromOrderedOneToMany)
        {
            List<string> forkJoinParams = new();
            List<string> formGroupInitialValues = new();
            List<string> newEntityInitValues = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                forkJoinParams.Add($$"""
                    default{{property.Name}}For{{entity.Name}}: this.apiService.getDefault{{property.Name}}For{{entity.Name}}(),
""");

                newEntityInitValues.Add($$"""
                        {{property.Name.FirstCharToLower()}}: data.default{{property.Name}}For{{entity.Name}}
""");
            }

            foreach (SpiderlyProperty orderedProp in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass childEntity = Helpers.GetEntityByPropertyType(orderedProp, allEntities);

                foreach (SpiderlyProperty m2mProp in childEntity.GetComplexManyToManyListProperties())
                {
                    string orderedFormArray = NgDetailsPropertyBlockGenerator.GetOrderedOneToManyFormArray(entity, orderedProp, isFromOrderedOneToMany);

                    formGroupInitialValues.Add($$"""
                    {{orderedFormArray}}.formGroupInitialValues = {
                        ...{{orderedFormArray}}.formGroupInitialValues,
                        {{m2mProp.Name.FirstCharToLower()}}: data.default{{m2mProp.Name}}For{{childEntity.Name}}
                    };
""");
                }

                var nested = CollectComplexManyToManyListInfo(childEntity, allEntities, isFromOrderedOneToMany: true);
                forkJoinParams.AddRange(nested.ForkJoinParams);
                formGroupInitialValues.AddRange(nested.FormGroupInitialValues);
            }

            return (forkJoinParams, formGroupInitialValues, newEntityInitValues);
        }

        internal static List<string> GetSimpleManyToManyTableLazyLoadCols(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (UITableColumn col in property.GetUITableColumns())
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
                SpiderlyProperty? extractedEntityProperty = extractedEntity?.Properties?.Where(x => x.Name == col.Field.Replace("DisplayName", "").Replace("CommaSeparated", ""))?.SingleOrDefault();

                SpiderlyClass extractedDTO = customDTOClasses.Where(x => x.Name == $"{Helpers.ExtractTypeFromGenericType(property.Type)}DTO").SingleOrDefault();
                SpiderlyProperty? extractedDTOProperty = extractedDTO?.Properties?.Where(x => x.Name == col.Field)?.SingleOrDefault();

                // col.Field is a hand-authored attribute string, matched against nothing until here — a typo
                // or a rename that missed it resolves to nothing on either the entity or its DTO.
                SpiderlyProperty? resolvedProperty = extractedEntityProperty ?? extractedDTOProperty;

                if (resolvedProperty == null || extractedEntity == null)
                {
                    throw SpiderlyDiagnostics.Create(
                        SpiderlyDiagnostics.UITableColumnFieldNotFound,
                        property.Location,
                        col.Field, property.Name, property.EntityName ?? entity.Name, Helpers.ExtractTypeFromGenericType(property.Type));
                }


                result.Add($$"""
                {name: this.translocoService.translate('{{col.TranslationKey}}'), filterType: '{{GetTableColFilterType(resolvedProperty)}}', field: '{{col.Field.FirstCharToLower()}}' {{GetTableColAdditionalProperties(resolvedProperty, extractedEntity)}} }
""");
            }

            return result;
        }

        internal static string? GetTableColAdditionalProperties(SpiderlyProperty property, SpiderlyClass entity)
        {
            // `dropdownOrMultiselectValues` maps the CELL value to its label — it is not filter
            // wiring, so it survives the header-filter deletion. `filterField`/`showMatchModes`
            // used to be emitted here and died with `p-columnFilter`; the generated details-table
            // has no filter surface until it scaffolds a filter store (tracked upstream).
            if (property.IsDropdownControlType())
                return $", dropdownOrMultiselectValues: await firstValueFrom(getPrimengDropdownNamebookOptions(this.apiService.get{property.Name}DropdownListFor{entity.Name}))";

            if (property.HasGenerateCommaSeparatedDisplayNameAttribute())
                return $", dropdownOrMultiselectValues: await firstValueFrom(getPrimengDropdownNamebookOptions(this.apiService.get{property.Name}DropdownListFor{entity.Name}))";

            if (property.Type.ScalarKind == SpiderlyScalarKind.Decimal)
            {
                string? decimalScale = property.GetDecimalScale();
                if (decimalScale != null)
                    return $", decimalPlaces: {decimalScale}";
            }

            return null;
        }

        internal static string? GetTableColFilterType(SpiderlyProperty property)
        {
            if (property.IsDropdownControlType())
                return "multiselect";

            if (property.HasGenerateCommaSeparatedDisplayNameAttribute())
                return "multiselect";

            // FK-bearing reference navs filter their grid column on the flattened {Nav}DisplayName string.
            if (property.IsForeignKeyReferenceNav())
                return "text";

            switch (property.Type.ScalarKind)
            {
                case SpiderlyScalarKind.String:
                    return "text";
                case SpiderlyScalarKind.Boolean:
                    return "boolean";
                case SpiderlyScalarKind.DateTime:
                case SpiderlyScalarKind.DateOnly:
                    return "date";
                case SpiderlyScalarKind.TimeOnly:
                    return "text";
                case SpiderlyScalarKind.Integer:
                case SpiderlyScalarKind.Decimal:
                    return "numeric";
                default:
                    break;
            }

            return null;
        }
    }
}
