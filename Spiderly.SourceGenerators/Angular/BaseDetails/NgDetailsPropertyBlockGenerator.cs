using Spiderly.SourceGenerators.Enums;
using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Spiderly.SourceGenerators.Angular
{
    internal static class NgDetailsPropertyBlockGenerator
    {
        internal static List<string> GetPropertyBlocks(
            List<SpiderlyProperty> properties,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses,
            bool isFromOrderedOneToMany
        )
        {
            List<string> result = new();

            SpiderlyClass customDTOClass = customDTOClasses.SingleOrDefault(x => SpiderlyNaming.IsGeneratedDTOName(x.Name, entity.Name, "DTO"));

            if (customDTOClass != null)
                properties.AddRange(customDTOClass.Properties);

            foreach (SpiderlyProperty property in GetOrderedPropertiesForUIBlocks(properties, entity, allEntities))
            {
                result.Add(GetSinglePropertyBlock(property, entity, allEntities, customDTOClasses, isFromOrderedOneToMany));
            }

            return result;
        }

        /// <summary>
        /// Builds the HTML block for a single property (normal control, ordered-one-to-many panel,
        /// or complex-many-to-many list panel). Shared by the flat-grid generator and the grouped generator.
        /// </summary>
        private static string GetSinglePropertyBlock(
            SpiderlyProperty property,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses,
            bool isFromOrderedOneToMany
        )
        {
            if (property.Attributes.Any(x => x.Name == "UIOrderedOneToMany"))
                return GetOrderedOneToManyBlock(entity, property, allEntities, customDTOClasses, isFromOrderedOneToMany);

            if (property.HasComplexManyToManyListAttribute())
                return GetComplexManyToManyListBlock(entity, property, allEntities, isFromOrderedOneToMany);

            string controlType = GetUIStringControlType(GetUIControlType(property));

            return $$"""
                    <div {{GetNgIfForPropertyBlock(property, isFromOrderedOneToMany)}} class="{{GetUIControlWidth(property, isFromOrderedOneToMany)}}">
                        <{{controlType}} {{GetControlAttributes(property, entity, isFromOrderedOneToMany)}}></{{controlType}}>
                        <ng-content select="[below{{property.Name}}For{{entity.Name}}]"></ng-content>
                    </div>
""";
        }

        /// <summary>
        /// Reads the <c>[UISection("...")]</c> section name (used as a Transloco key) for a property,
        /// or <c>null</c> when the property is not assigned to a section.
        /// </summary>
        internal static string? GetUISectionName(SpiderlyProperty property)
        {
            return property.Attributes.SingleOrDefault(x => x.Name == "UISection")?.Value;
        }

        /// <summary>
        /// True when at least one property included in the details UI declares <c>[UISection]</c>.
        /// When false, the details page renders as a single flat grid for backward compatibility.
        /// </summary>
        internal static bool HasAnyUISection(
            List<SpiderlyProperty> properties,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses
        )
        {
            List<SpiderlyProperty> allProperties = properties.ToList();
            SpiderlyClass customDTOClass = customDTOClasses.SingleOrDefault(x => SpiderlyNaming.IsGeneratedDTOName(x.Name, entity.Name, "DTO"));
            if (customDTOClass != null)
                allProperties.AddRange(customDTOClass.Properties);

            return GetOrderedPropertiesForUIBlocks(allProperties, entity, allEntities).Any(x => GetUISectionName(x) != null);
        }

        /// <summary>
        /// Groups property blocks into sections (panels) for the details page. Returns one entry per
        /// section in first-appearance order; properties without <c>[UISection]</c> collapse into a single
        /// implicit headerless section (<c>null</c> key), positioned by the first such property's appearance.
        /// </summary>
        internal static List<DetailsFieldGroup> GetGroupedPropertyBlocks(
            List<SpiderlyProperty> properties,
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses
        )
        {
            List<SpiderlyProperty> allProperties = properties.ToList();
            SpiderlyClass customDTOClass = customDTOClasses.SingleOrDefault(x => SpiderlyNaming.IsGeneratedDTOName(x.Name, entity.Name, "DTO"));
            if (customDTOClass != null)
                allProperties.AddRange(customDTOClass.Properties);

            List<DetailsFieldGroup> groups = new();

            foreach (SpiderlyProperty property in GetOrderedPropertiesForUIBlocks(allProperties, entity, allEntities))
            {
                string? groupKey = GetUISectionName(property);

                DetailsFieldGroup group = groups.FirstOrDefault(x => x.TranslationKey == groupKey);
                if (group == null)
                {
                    group = new DetailsFieldGroup { TranslationKey = groupKey };
                    groups.Add(group);
                }

                group.Blocks.Add(GetSinglePropertyBlock(property, entity, allEntities, customDTOClasses, isFromOrderedOneToMany: false));
            }

            return groups;
        }

        internal static List<PropertyWithContext> GetAllPropertiesWithContext(
            SpiderlyClass entity,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses
        )
        {
            List<PropertyWithContext> result = new();

            List<SpiderlyProperty> properties = entity.Properties.ToList();
            SpiderlyClass customDTOClass = customDTOClasses.SingleOrDefault(x => SpiderlyNaming.IsGeneratedDTOName(x.Name, entity.Name, "DTO"));
            if (customDTOClass != null)
                properties = properties.Concat(customDTOClass.Properties).ToList();

            List<SpiderlyProperty> orderedProperties = GetOrderedPropertiesForUIBlocks(properties, entity, allEntities);

            foreach (SpiderlyProperty property in orderedProperties)
            {
                if (property.HasUIOrderedOneToManyAttribute())
                {
                    result.Add(new PropertyWithContext
                    {
                        Property = property,
                        Entity = entity,
                        FormControlName = null // an ordered-O2M entry carries no control name
                    });

                    SpiderlyClass nestedEntity = allEntities.SingleOrDefault(
                        x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type)
                    );
                    result.AddRange(GetAllPropertiesWithContext(nestedEntity, allEntities, customDTOClasses));

                    continue;
                }

                if (property.HasComplexManyToManyListAttribute())
                {
                    result.Add(new PropertyWithContext
                    {
                        Property = property,
                        Entity = entity,
                        FormControlName = null // a complex-M2M-list entry carries no control name
                    });

                    continue;
                }

                UIControlTypeCodes controlType = GetUIControlType(property);
                string? formControlName = controlType == UIControlTypeCodes.Table
                    ? null
                    : GetFormControlName(property);

                result.Add(new PropertyWithContext
                {
                    Property = property,
                    Entity = entity,
                    FormControlName = formControlName
                });
            }

            return result;
        }

        /// <summary>
        /// The properties of <paramref name="entity"/> (including flattened nested ones) that render as an
        /// <b>enum</b> dropdown — i.e. <c>IsEnum</c> and resolving to <see cref="UIControlTypeCodes.Dropdown"/>.
        /// These get an <c>optionsFor{Entity}</c> variable but, unlike FK dropdowns, no backend list endpoint;
        /// their options are populated client-side from the generated TS enum. Single source of truth shared by
        /// the option-population emitter and the enum-helper import emitter so the two can't drift.
        /// </summary>
        internal static List<PropertyWithContext> GetEnumDropdownContexts(SpiderlyClass entity, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            return GetAllPropertiesWithContext(entity, entities, customDTOClasses)
                .Where(x => x.FormControlName != null
                         && x.Property.IsEnum
                         && GetUIControlType(x.Property) == UIControlTypeCodes.Dropdown)
                .ToList();
        }

        internal static UIControlTypeCodes GetUIControlType(SpiderlyProperty property)
        {
            SpiderlyAttribute uiControlTypeAttribute = property.Attributes.SingleOrDefault(x => x.Name == "UIControlType");

            if (uiControlTypeAttribute != null)
            {
                Enum.TryParse(uiControlTypeAttribute.Value, out UIControlTypeCodes parseResult);
                return parseResult;
            }

            if (property.IsBlob())
                return UIControlTypeCodes.File;

            // Enum-typed entity properties render as a dropdown bound to the TS enum (no API round-trip).
            // Must come before the M2O check because IsEnum short-circuits IsManyToOneType for the same property.
            if (property.IsEnum)
                return UIControlTypeCodes.Dropdown;

            // FK-bearing reference navs (M2O + 1-1 dependent) render as the reference autocomplete.
            if (property.IsForeignKeyReferenceNav())
                return UIControlTypeCodes.Autocomplete;

            if (property.HasSimpleManyToManyTableLazyLoadAttribute())
                return UIControlTypeCodes.Table;

            if (property.HasComplexManyToManyReadonlyTableAttribute())
                return UIControlTypeCodes.Table;

            switch (property.Type.ScalarKind)
            {
                case SpiderlyScalarKind.String:
                    return UIControlTypeCodes.TextBox;
                case SpiderlyScalarKind.Boolean:
                    return UIControlTypeCodes.CheckBox;
                case SpiderlyScalarKind.DateTime:
                case SpiderlyScalarKind.DateOnly:
                case SpiderlyScalarKind.TimeOnly:
                    return UIControlTypeCodes.Calendar;
                case SpiderlyScalarKind.Decimal:
                    return UIControlTypeCodes.Decimal;
                case SpiderlyScalarKind.Integer:
                    return UIControlTypeCodes.Integer;
                default:
                    break;
            }

            return UIControlTypeCodes.None; // Note: We can't throw exception here
        }

        internal static string GetUIStringControlType(UIControlTypeCodes controlType)
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
                case UIControlTypeCodes.Markdown:
                    return "spiderly-markdown";
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
                case UIControlTypeCodes.TextBox:
                    return "spiderly-textbox";
                case UIControlTypeCodes.Table:
                    return "spiderly-data-table";
                default:
                    return $"Unknown UIControlType: '{controlType}'.";

            }
        }

        internal static string? GetFormControlName(SpiderlyProperty property)
        {
            // Enum-typed properties bind directly to the property name (the DTO field is the enum value, not a synthesized FK).
            if (property.IsEnum)
                return property.Name.FirstCharToLower();

            // FK-bearing reference navs bind to the flattened {Nav}Id the DTO carries, not the raw nav name.
            if (property.IsForeignKeyReferenceNav())
                return $"{property.Name.FirstCharToLower()}Id";

            if (property.IsMultiSelectControlType())
                return $"selected{property.Name}Ids";

            if (property.IsMultiAutocompleteControlType())
                return $"selected{property.Name}NamebookDTOList";

            return property.Name.FirstCharToLower();
        }

        internal static string GetMainDTOFormGroupForMainUIForm(SpiderlyClass entity, bool isFromOrderedOneToMany, bool isControlDirectlyOnParent = false)
        {
            string formGroup;

            if (isFromOrderedOneToMany)
                formGroup = $"{entity.Name.FirstCharToLower()}FormGroup";
            else
                formGroup = $"this.parentFormGroup";

            if (isControlDirectlyOnParent)
                return formGroup;

            return $"{formGroup}.controls.{entity.Name.FirstCharToLower()}DTO";
        }

        internal static List<SpiderlyProperty> GetOrderedPropertiesForUIBlocks(List<SpiderlyProperty> properties, SpiderlyClass entity, List<SpiderlyClass> allEntities)
        {
            List<SpiderlyProperty> orderedProperties = properties
                // The principal side of a 1-1 is a bare M2O-shaped nav that would otherwise render as an
                // Autocomplete bound to a {Nav}Id control the DTO never carries. Excluded here (and in
                // GetAllPropertiesWithContext) — the only two chokepoints that emit bare reference navs.
                .Where(x => x.IsIncludedInDetailsUi(entity) && !x.IsOneToOnePrincipalInverse(entity, allEntities))
                .OrderBy(x =>
                    x.IsBlob() ? 0 :
                    x.Attributes.Any(attr => attr.Value == UIControlTypeCodes.TextArea.ToString()) ? 2 :
                    x.Attributes.Any(attr => attr.Value == UIControlTypeCodes.Editor.ToString()) ? 3 :
                    x.Attributes.Any(attr => attr.Value == UIControlTypeCodes.Markdown.ToString()) ? 3 :
                    x.Attributes.Any(attr => attr.Name == "UIOrderedOneToMany") ? 4 :
                    x.Attributes.Any(attr => attr.Name == "SimpleManyToManyTableLazyLoad") ? 5 :
                    x.Attributes.Any(attr => attr.Name == "ComplexManyToManyList") ? 6
                    : 1)
                .ToList();

            return orderedProperties;
        }

        internal static string GetOrderedOneToManyFormArray(SpiderlyClass entity, SpiderlyProperty property, bool isFromOrderedOneToMany)
        {
            if (isFromOrderedOneToMany)
                return $"{entity.Name.FirstCharToLower()}FormGroup.controls.ordered{property.Name}SaveBodyDTO";
            else
                return $"this.parentFormGroup.controls.ordered{property.Name}SaveBodyDTO";
        }

        /// <summary>
        /// </summary>
        /// <param name="property">eg. List<SegmentationItem> SegmentationItems</param>
        /// <param name="allEntities"></param>
        /// <param name="customDTOClasses"></param>
        /// <returns></returns>
        private static string GetOrderedOneToManyBlock(
            SpiderlyClass entity,
            SpiderlyProperty property,
            List<SpiderlyClass> allEntities,
            List<SpiderlyClass> customDTOClasses,
            bool isFromOrderedOneToMany
        )
        {
            SpiderlyClass extractedEntity = Helpers.GetEntityByPropertyType(property, allEntities); // eg. SegmentationItem

            // Drop the child's M2O navigation that points back to the parent (we render that
            // inline form INSIDE the parent's details page, so the back-reference would be
            // redundant). Match BOTH WithMany() == parent collection name AND the property's
            // type == parent entity name — the WithMany name alone collides whenever any other
            // M2O on the child happens to use the same collection name on its own side
            // (e.g. ProjectTask.Project has WithMany("ProjectTasks"); ProjectTask.TaskCategory
            // also has WithMany("ProjectTasks") because TaskCategory has its own ProjectTasks
            // collection — checking name alone silently dropped TaskCategory's dropdown).
            List<SpiderlyProperty> propertyBlocks = extractedEntity.Properties
                .Where(x => !(x.WithMany() == property.Name && x.Type.Name == entity.Name))
                .ToList();

            return $$"""
                     <div class="col-8">
                        <spiderly-panel [toggleable]="true" [collapsed]="{{property.Name.FirstCharToLower()}}For{{entity.Name}}PanelCollapsed">
                            <panel-header [title]="t('{{property.Name}}')" icon="pi pi-list"></panel-header>
                            <panel-body [normalBottomPadding]="true">
                                @for ({{extractedEntity.Name.FirstCharToLower()}}FormGroup of {{GetOrderedOneToManyFormArray(entity, property, isFromOrderedOneToMany)}}.getFormGroups(); track {{extractedEntity.Name.FirstCharToLower()}}FormGroup.trackingId; let index = $index; let last = $last) {
                                    <index-card
                                    [index]="index"
                                    [last]="false"
                                    [crudMenu]="{{GetOrderedOneToManyFormArray(entity, property, isFromOrderedOneToMany)}}.getCrudMenuForOrderedData()"
                                    [showCrudMenu]="isAuthorizedForSave"
                                    (onMenuIconClick)="{{GetOrderedOneToManyFormArray(entity, property, isFromOrderedOneToMany)}}.lastMenuIconIndexClicked = $event"
                                    >
                                        <form [formGroup]="{{extractedEntity.Name.FirstCharToLower()}}FormGroup" class="spiderly-grid">
{{string.Join("\n", GetPropertyBlocks(propertyBlocks, extractedEntity, allEntities, customDTOClasses, isFromOrderedOneToMany: true))}}
                                            <ng-container *ngIf="additionalContentTemplateFor{{property.Name}}For{{entity.Name}}">
                                                <ng-container *ngTemplateOutlet="additionalContentTemplateFor{{property.Name}}For{{entity.Name}}; context: { $implicit: {{extractedEntity.Name.FirstCharToLower()}}FormGroup, formGroup: {{extractedEntity.Name.FirstCharToLower()}}FormGroup, index: index, last: last }"></ng-container>
                                            </ng-container>
                                        </form>
                                    </index-card>
                                }

                                <div class="panel-add-button">
                                    <spiderly-button [disabled]="!isAuthorizedForSave" (onClick)="{{GetOrderedOneToManyFormArray(entity, property, isFromOrderedOneToMany)}}.addNewFormGroup(null)" [label]="t('AddNew{{Helpers.ExtractTypeFromGenericType(property.Type)}}')" icon="pi pi-plus"></spiderly-button>
                                </div>

                            </panel-body>
                        </spiderly-panel>
                    </div>
""";
        }

        internal static (SpiderlyClass JunctionEntity, SpiderlyProperty CurrentSideM2MProperty, SpiderlyProperty OtherSideM2MProperty, SpiderlyClass OtherSideEntity) ResolveComplexManyToManyListInfo(
            SpiderlyClass entity,
            SpiderlyProperty property,
            List<SpiderlyClass> allEntities
        )
        {
            SpiderlyClass junctionEntity = allEntities.Single(x => x.Name == Helpers.ExtractTypeFromGenericType(property.Type));

            SpiderlyProperty currentSideM2MProperty = junctionEntity.Properties
                .Single(x =>
                    x.HasM2MWithManyAttribute() &&
                    x.Type.Name == entity.Name &&
                    x.Attributes.Any(a => a.Name == "M2MWithMany" && a.Value == property.Name)
                );

            SpiderlyProperty otherSideM2MProperty = junctionEntity.Properties
                .Where(x => x.HasM2MWithManyAttribute())
                .Single(x => x != currentSideM2MProperty);

            SpiderlyClass otherSideEntity = allEntities.Single(x => x.Name == otherSideM2MProperty.Type.Name);

            return (junctionEntity, currentSideM2MProperty, otherSideM2MProperty, otherSideEntity);
        }

        internal static string GetComplexManyToManyListBlock(
            SpiderlyClass entity,
            SpiderlyProperty property,
            List<SpiderlyClass> allEntities,
            bool isFromOrderedOneToMany
        )
        {
            var (junctionEntity, currentSideM2MProperty, otherSideM2MProperty, otherSideEntity) = ResolveComplexManyToManyListInfo(entity, property, allEntities);

            string formArrayAccess = GetComplexManyToManyListFormArray(entity, property, isFromOrderedOneToMany);
            string junctionFormGroupVar = $"{junctionEntity.Name.FirstCharToLower()}FormGroup";

            string currentSideFKName = $"{currentSideM2MProperty.Name}Id";
            string otherSideFKName = $"{otherSideM2MProperty.Name}Id";

            // The FK scalars are the row's identity, not editable data: the other side already names
            // the card (its DisplayName header) and the current side is the form's own parent. They
            // stay in the form model for the save round-trip — they just don't render as inputs.
            List<SpiderlyProperty> additionalFields = junctionEntity.Properties
                .Where(p => !p.IsManyToOneType() && !p.Type.IsOneToManyType() && !p.HasUIDoNotGenerateAttribute())
                .Where(p => p.Name != currentSideFKName && p.Name != otherSideFKName)
                .ToList();

            StringBuilder fieldsHtml = new();
            foreach (SpiderlyProperty field in additionalFields)
            {
                UIControlTypeCodes controlType = GetUIControlType(field);
                string controlTag = GetUIStringControlType(controlType);
                string? formControlName = field.Name.FirstCharToLower();

                string controlAttr = $"[control]=\"{junctionFormGroupVar}.getControl('{formControlName}')\"";

                if (controlType == UIControlTypeCodes.Decimal)
                    controlAttr += $" [decimal]=\"true\" [maxFractionDigits]=\"{field.GetDecimalScale()}\"";

                fieldsHtml.AppendLine($$"""
                                            <div class="col-8">
                                                <{{controlTag}} {{controlAttr}}></{{controlTag}}>
                                            </div>
""");
            }

            return $$"""
                     <div class="col-8">
                        <spiderly-panel [toggleable]="true" [collapsed]="{{property.Name.FirstCharToLower()}}For{{entity.Name}}PanelCollapsed">
                            <panel-header [title]="t('{{property.Name}}')" icon="pi pi-list"></panel-header>
                            <panel-body [normalBottomPadding]="true">
                                @for ({{junctionFormGroupVar}} of {{formArrayAccess}}.getFormGroups(); track {{junctionFormGroupVar}}.trackingId; let index = $index) {
                                    <index-card
                                    [index]="index"
                                    [last]="false"
                                    [header]="{{junctionFormGroupVar}}.getControl('{{otherSideM2MProperty.Name.FirstCharToLower()}}DisplayName')?.getRawValue()"
                                    [showCrudMenu]="false"
                                    >
                                        <form [formGroup]="{{junctionFormGroupVar}}" class="spiderly-grid">
{{fieldsHtml}}
                                        </form>
                                    </index-card>
                                }

                            </panel-body>
                        </spiderly-panel>
                    </div>
""";
        }

        private static string GetComplexManyToManyListFormArray(SpiderlyClass entity, SpiderlyProperty property, bool isFromOrderedOneToMany)
        {
            if (isFromOrderedOneToMany)
                return $"{entity.Name.FirstCharToLower()}FormGroup.controls.{property.Name.FirstCharToLower()}";
            else
                return $"this.parentFormGroup.controls.{property.Name.FirstCharToLower()}";
        }

        private static string GetControlAttributes(SpiderlyProperty property, SpiderlyClass entity, bool isFromOrderedOneToMany)
        {
            UIControlTypeCodes controlType = GetUIControlType(property);

            if (controlType == UIControlTypeCodes.Decimal)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [decimal]=\"true\" [maxFractionDigits]=\"{property.GetDecimalScale()}\"";
            }
            else if (controlType == UIControlTypeCodes.Calendar)
            {
                string control = $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\"";

                if (property.Type.IsDateOnly())
                    return $"{control} [dateOnly]=\"true\"";
                if (property.Type.IsTimeOnly())
                    return $"{control} [timeOnly]=\"true\"";
                return $"{control} [showTime]=\"showTimeOn{property.Name}For{entity.Name}\"";
            }
            else if (controlType == UIControlTypeCodes.CheckBox)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" (onChange)=\"on{property.Name}For{entity.Name}Change.next($event)\"";
            }
            else if (controlType == UIControlTypeCodes.File)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [fileData]=\"{GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany)}.controls.{property.Name.FirstCharToLower()}Data.getRawValue()\" [objectId]=\"{GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany)}.controls.id.getRawValue()\" (onFileSelected)=\"upload{property.Name}For{entity.Name}($event, {GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany)})\" [disabled]=\"!isAuthorizedForSave\" [isUrlFileData]=\"{property.IsPublicUrl().ToString().ToLower()}\" {GetImageDimensionsHtmlAttributes(property)} {GetAcceptedFileTypesHtmlAttribute(property)} {GetMaxFileSizeHtmlAttribute(property)} ";
            }
            else if (controlType == UIControlTypeCodes.Dropdown)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" (onChange)=\"on{property.Name}For{entity.Name}Change.next($event)\"";
            }
            else if (controlType == UIControlTypeCodes.Autocomplete)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" [displayName]=\"{GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany)}.controls.{property.Name.FirstCharToLower()}DisplayName.getRawValue()\" (onTextInput)=\"search{property.Name}For{entity.Name}($event)\" ";
            }
            else if (controlType == UIControlTypeCodes.MultiSelect)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany, isControlDirectlyOnParent: true)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" [label]=\"t('{property.Name}')\" ";
            }
            else if (controlType == UIControlTypeCodes.MultiAutocomplete)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany, isControlDirectlyOnParent: true)}\" [options]=\"{property.Name.FirstCharToLower()}OptionsFor{entity.Name}\" (onTextInput)=\"search{property.Name}For{entity.Name}($event)\" [label]=\"t('{property.Name}')\" ";
            }
            else if (property.HasSimpleManyToManyTableLazyLoadAttribute())
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
            else if (property.HasComplexManyToManyReadonlyTableAttribute())
            {
                return $$"""

                            [tableTitle]="t('{{property.Name}}')"
                            [cols]="{{property.Name.FirstCharToLower()}}TableColsFor{{entity.Name}}"
                            [getPaginatedListObservableMethod]="getPaginated{{property.Name}}ListObservableMethodFor{{entity.Name}}"
                            [exportListToExcelObservableMethod]="export{{property.Name}}ListToExcelObservableMethodFor{{entity.Name}}"
                            [showAddButton]="false"
                            [readonly]="true"
""";
            }
            else if (controlType == UIControlTypeCodes.ColorPicker)
            {
                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [showInputTextField]=\"show{property.Name}TextFieldFor{entity.Name}\"";
            }
            else if (controlType == UIControlTypeCodes.Editor || controlType == UIControlTypeCodes.Markdown)
            {
                if (property.HasS3PublicStorageAttribute())
                {
                    // acceptedFileTypes drives the editor's file-picker accept attribute; the markdown
                    // control has no picker (paste-only uploads), so it gets no such binding.
                    string acceptedFileTypes = controlType == UIControlTypeCodes.Editor
                        ? GetAcceptedFileTypesHtmlAttribute(property)
                        : "";

                    return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\" [uploadImageMethod]=\"upload{property.Name}ImageFor{entity.Name}\" [objectId]=\"{GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany)}.controls.id.getRawValue()\" {acceptedFileTypes}";
                }

                return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\"";
            }

            return $"[control]=\"{GetControlHtmlAttributeValue(property, entity, isFromOrderedOneToMany)}\"";
        }

        private static string GetControlHtmlAttributeValue(SpiderlyProperty property, SpiderlyClass entity, bool isFromOrderedOneToMany, bool isControlDirectlyOnParent = false)
        {
            return $"{GetMainDTOFormGroupForMainUIForm(entity, isFromOrderedOneToMany, isControlDirectlyOnParent)}.getControl('{GetFormControlName(property)}')";
        }

        internal static string? GetUIControlWidth(SpiderlyProperty property, bool isFromOrderedOneToMany)
        {
            SpiderlyAttribute uiControlWidthAttribute = property.Attributes.SingleOrDefault(x => x.Name == "UIControlWidth");

            if (uiControlWidthAttribute != null)
                return uiControlWidthAttribute.Value;

            UIControlTypeCodes controlType = GetUIControlType(property);

            if (controlType == UIControlTypeCodes.File ||
                controlType == UIControlTypeCodes.TextArea ||
                controlType == UIControlTypeCodes.MultiSelect ||
                controlType == UIControlTypeCodes.MultiAutocomplete ||
                controlType == UIControlTypeCodes.Table ||
                controlType == UIControlTypeCodes.Editor ||
                controlType == UIControlTypeCodes.Markdown)
            {
                return "col-8";
            }

            if (isFromOrderedOneToMany)
                return "col-8";

            return "col-8 md:col-4";
        }

        private static string? GetNgIfForPropertyBlock(SpiderlyProperty property, bool isFromOrderedOneToMany)
        {
            if (isFromOrderedOneToMany)
                return null;

            return $$"""
*ngIf="show{{property.Name}}For{{property.EntityName}}"
""";
        }

        internal static string GetImageDimensionsHtmlAttributes(SpiderlyProperty property)
        {
            int imageWidth = property.GetImageWidth();
            int imageHeight = property.GetImageHeight();

            List<string> attributes = new();

            if (imageWidth > 0)
                attributes.Add($"[imageWidth]=\"{imageWidth}\"");
            if (imageHeight > 0)
                attributes.Add($"[imageHeight]=\"{imageHeight}\"");

            if (attributes.Count == 0)
                return "";

            return string.Join(" ", attributes);
        }

        internal static string GetAcceptedFileTypesHtmlAttribute(SpiderlyProperty property)
        {
            List<string>? fileTypes = property.GetAcceptedFileTypes();

            if (fileTypes == null || fileTypes.Count == 0)
                return "";

            string arrayLiteral = "[" + string.Join(", ", fileTypes.Select(t => $"'{t}'")) + "]";
            return $"[acceptedFileTypes]=\"{arrayLiteral}\"";
        }

        internal static string GetMaxFileSizeHtmlAttribute(SpiderlyProperty property)
        {
            int maxFileSize = property.GetMaxFileSize();

            if (maxFileSize <= 0)
                return "";

            return $"[maxFileSize]=\"{maxFileSize}\"";
        }
    }
}
