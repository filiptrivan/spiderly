using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    internal static class NgDetailsVariableGenerator
    {
        internal static string GetShowFormBlocksVariables(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            StringBuilder sb = new();

            List<PropertyWithContext> contexts = NgDetailsPropertyBlockGenerator.GetAllPropertiesWithContext(entity, entities, customDTOClasses);

            foreach (PropertyWithContext context in contexts)
            {
                sb.AppendLine($$"""
    @Input() show{{context.Property.Name}}For{{context.Entity.Name}} = true;
""");
            }

            return sb.ToString();
        }

        internal static string GetHelperVariables(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            StringBuilder sb = new();

            List<PropertyWithContext> contexts = NgDetailsPropertyBlockGenerator.GetAllPropertiesWithContext(entity, entities, customDTOClasses);

            foreach (PropertyWithContext context in contexts.Where(x => x.FormControlName != null))
            {
                UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(context.Property);

                if (controlType == UIControlTypeCodes.Dropdown)
                {
                    sb.AppendLine($$"""
    @Output() on{{context.Property.Name}}For{{context.Entity.Name}}Change = new EventEmitter<DropdownChangeEvent>();
""");
                }
                else if (controlType == UIControlTypeCodes.Calendar && context.Property.Type.IsDateTime())
                {
                    sb.AppendLine($$"""
    @Input() showTimeOn{{context.Property.Name}}For{{context.Entity.Name}} = false;
""");
                }
                else if (controlType == UIControlTypeCodes.CheckBox)
                {
                    sb.AppendLine($$"""
    @Output() on{{context.Property.Name}}For{{context.Entity.Name}}Change = new EventEmitter<CheckboxChangeEvent>();
""");
                }
                else if (controlType == UIControlTypeCodes.ColorPicker)
                {
                    sb.AppendLine($$"""
    @Input() show{{context.Property.Name}}TextFieldFor{{context.Entity.Name}} = true;
""");
                }
            }

            return sb.ToString();
        }

        internal static string GetAdditionalPermissionCodes(SpiderlyClass entity)
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

        internal static List<string> GetManyToManyTableVariables(SpiderlyClass entity, List<SpiderlyClass> entities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties)
            {
                if (
                    property.HasSimpleManyToManyTableLazyLoadAttribute() ||
                    property.HasComplexManyToManyReadonlyTableAttribute()
                )
                {
                    SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, entities);

                    result.Add($$"""
    {{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}}: Column<{{extractedEntity.Name}}>[];
    getPaginated{{property.Name}}ListObservableMethodFor{{entity.Name}} = this.apiService.getPaginated{{property.Name}}ListFor{{entity.Name}};
    export{{property.Name}}ListToExcelObservableMethodFor{{entity.Name}} = this.apiService.export{{property.Name}}ListToExcelFor{{entity.Name}};
""");
                }
            }

            return result;
        }

        internal static List<string> GetSimpleManyToManyTableLazyLoadVariables(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                result.Add($$"""
    newlySelected{{property.Name}}IdsFor{{entity.Name}}: number[] = [];
    unselected{{property.Name}}IdsFor{{entity.Name}}: number[] = [];
    areAll{{property.Name}}SelectedFor{{entity.Name}}: boolean = null;
    last{{property.Name}}LazyLoadTableFilterFor{{entity.Name}}: Filter;
""");
            }

            return result;
        }

        internal static List<string> GetPrimengOptionVariables(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            List<string> result = new();

            List<PropertyWithContext> contexts = NgDetailsPropertyBlockGenerator.GetAllPropertiesWithContext(entity, entities, customDTOClasses);

            foreach (PropertyWithContext context in contexts.Where(x => x.FormControlName != null))
            {
                UIControlTypeCodes controlType = NgDetailsPropertyBlockGenerator.GetUIControlType(context.Property);

                if (controlType == UIControlTypeCodes.Autocomplete ||
                    controlType == UIControlTypeCodes.Dropdown ||
                    controlType == UIControlTypeCodes.MultiAutocomplete ||
                    controlType == UIControlTypeCodes.MultiSelect)
                {
                    result.Add($$"""
    {{context.Property.Name.FirstCharToLower()}}OptionsFor{{context.Entity.Name}}: Namebook[];
""");
                }
            }

            return result;
        }

        internal static List<string> GetOrderedOneToManyVariables(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                result.Add($$"""
    @Input() {{property.Name.FirstCharToLower()}}For{{entity.Name}}PanelCollapsed: boolean = false;
    @Input() additionalContentTemplateFor{{property.Name}}For{{entity.Name}}: TemplateRef<any> | undefined;
""");

                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities);
                result.AddRange(GetOrderedOneToManyVariables(extractedEntity, allEntities));
            }

            return result;
        }

        internal static List<string> GetOrderedOneToManyForkJoinParameters(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = allEntities.Where(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)).Single();

                result.AddRange(GetOrderedOneToManyForkJoinParameters(extractedEntity, allEntities));

                result.Add($$"""
                    {{property.Name.FirstCharToLower()}}For{{entity.Name}}: this.apiService.getOrdered{{property.Name}}For{{entity.Name}}(this.modelId),
""");
            }

            return result;
        }

        internal static List<string> GetComplexManyToManyListVariables(SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.GetComplexManyToManyListProperties())
            {
                result.Add($$"""
    @Input() {{property.Name.FirstCharToLower()}}For{{entity.Name}}PanelCollapsed: boolean = false;
""");
            }

            foreach (SpiderlyProperty property in entity.GetOrderedOneToManyProperties())
            {
                SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities);
                result.AddRange(GetComplexManyToManyListVariables(extractedEntity, allEntities));
            }

            return result;
        }

        internal static List<string> GetSimpleManyToManyTableLazyLoadSaveBodyAssignements(SpiderlyClass entity)
        {
            List<string> result = new();

            foreach (SpiderlyProperty property in entity.Properties.Where(x => x.HasSimpleManyToManyTableLazyLoadAttribute()))
            {
                result.Add($$"""
        this.parentFormGroup.controls.selected{{property.Name}}Ids.setValue(this.newlySelected{{property.Name}}IdsFor{{entity.Name}});
        this.parentFormGroup.controls.unselected{{property.Name}}Ids.setValue(this.unselected{{property.Name}}IdsFor{{entity.Name}});
        this.parentFormGroup.controls.areAll{{property.Name}}Selected.setValue(this.areAll{{property.Name}}SelectedFor{{entity.Name}});
        this.parentFormGroup.controls.{{property.Name.FirstCharToLower()}}TableFilter.setValue(this.last{{property.Name}}LazyLoadTableFilterFor{{entity.Name}});
""");
            }

            return result;
        }
    }
}
