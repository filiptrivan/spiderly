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

        internal static List<string> GetManyToManyTableColsInitializations(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
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

        internal static List<string> GetManyToManyMultiSelectListForDropdownMethods(SpiderlyClass entity, List<SpiderlyClass> entities, bool isFromOrderedOneToMany = false)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasUIDoNotGenerateAttribute() == false))
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    result.AddRange(GetManyToManyMultiSelectListForDropdownMethods(extractedEntity, entities, isFromOrderedOneToMany: true));

                    continue;
                }

                if (property.IsMultiSelectControlType() == false && property.IsDropdownControlType() == false)
                    continue;

                string idArgument = isFromOrderedOneToMany ? "null" : "this.modelId";

                result.Add($$"""
            this.apiService.get{{property.Name}}DropdownListFor{{entity.Name}}({{idArgument}}).subscribe(no => {
                this.{{property.Name.FirstCharToLower()}}OptionsFor{{entity.Name}} = no;
            });
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
    search{{context.Property.Name}}For{{context.Entity.Name}}(event: AutoCompleteCompleteEvent, modelId: number = null) {
        this.apiService.get{{context.Property.Name}}AutocompleteListFor{{context.Entity.Name}}(50, event?.query ?? '', modelId).subscribe(no => {
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

            foreach (SpiderlyProperty property in properties.Where(x => x.HasUIDoNotGenerateAttribute() == false))
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

            foreach (SpiderlyProperty property in properties.Where(x => x.HasUIDoNotGenerateAttribute() == false))
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
                else if (controlType == UIControlTypeCodes.Editor && property.HasS3PublicStorageAttribute())
                {
                    result.Add($$"""
    upload{{property.Name}}ImageFor{{entity.Name}} = (formData: FormData): Observable<string> => {
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

        private static List<string> GetSimpleManyToManyTableLazyLoadCols(SpiderlyProperty property, SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            foreach (UITableColumn col in property.GetUITableColumns())
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);
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
                case "DateOnly":
                case "DateOnly?":
                case "TimeOnly":
                case "TimeOnly?":
                    return ", showMatchModes: true";
                case "decimal":
                case "decimal?":
                case "float":
                case "float?":
                case "double":
                case "double?":
                    string decimalScale = property.GetDecimalScale();
                    return decimalScale != null
                        ? $", showMatchModes: true, decimalPlaces: {decimalScale}"
                        : ", showMatchModes: true";
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

            if (property.IsManyToOneType())
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
                case "DateOnly":
                case "DateOnly?":
                    return "date";
                case "TimeOnly":
                case "TimeOnly?":
                    return "text";
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
    }
}
