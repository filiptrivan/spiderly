using Spiderly.SourceGenerators.Models;
using Spiderly.SourceGenerators.Shared;
using System.Collections.Generic;
using System.Linq;

namespace Spiderly.SourceGenerators.Angular
{
    internal static class NgDetailsImportGenerator
    {
        internal static string GetImports(List<SpiderlyClass> customDTOClasses, List<SpiderlyClass> entities, List<SpiderlyClass> currentProjectEntities)
        {
            List<AngularImport> customDTOImports = customDTOClasses
                .Select(x => new AngularImport
                {
                    Namespace = x.Namespace.Replace(".DTO", ""),
                    Name = Helpers.RemoveDtoSuffix(x.Name)
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

            List<AngularImport> mainUIFormImports = entities
                .Select(x => new AngularImport
                {
                    Namespace = x.Namespace.Replace(".Entities", ""),
                    Name = $"{x.Name}MainUIForm"
                })
                .ToList();

            List<AngularImport> imports = customDTOImports.Concat(entityImports).Concat(saveBodyImports).Concat(mainUIFormImports).ToList();

            return $$"""
{{string.Join("\n", GetEnumNamebookListImports(currentProjectEntities, entities, customDTOClasses))}}
import { ValidatorService } from 'src/app/business/services/validators/validators';
import { DropdownChangeEvent } from 'primeng/dropdown';
import { CheckboxChangeEvent } from 'primeng/checkbox';
import { CommonModule } from '@angular/common';
import { FormsModule, ReactiveFormsModule } from '@angular/forms';
import { Component, EventEmitter, Input, Output, TemplateRef } from '@angular/core';
import { ApiService } from '../services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { AutoCompleteCompleteEvent } from 'primeng/autocomplete';
import { ActivatedRoute } from '@angular/router';
import { combineLatest, firstValueFrom, forkJoin, map, Observable, of, Subscription } from 'rxjs';
import { MenuItem } from 'primeng/api';
import { AuthService } from '../services/auth/auth.service';
import { SpiderlyControlsModule, CardSkeletonComponent, IndexCardComponent, IsAuthorizedForSaveEvent, SpiderlyDataTableComponent, SpiderlyFormArray, BaseEntity, LastMenuIconIndexClicked, SpiderlyFormGroup, SpiderlyButton, nameof, BaseFormService, Column, Filter, LazyLoadSelectedIdsResult, AllClickEvent, SpiderlyFileSelectEvent, getPrimengDropdownNamebookOptions, PrimengOption, SpiderlyFormControl, getPrimengAutocompleteNamebookOptions, SpiderlyPanelsModule, Namebook, EditorImageUploadResult } from 'spiderly';
{{string.Join("\n", GetDynamicNgImports(imports))}}
""";
        }

        /// <summary>
        /// Imports the generated <c>get{Enum}NamebookList</c> builder for every enum that renders as a dropdown on
        /// a generated entity component. Scoped to exactly the enums actually called (so no unused imports), using
        /// the same per-entity dropdown walk as the option-population emitter (<see cref="NgDetailsPropertyBlockGenerator.GetEnumDropdownContexts"/>)
        /// and the same <see cref="Extensions.GeneratesDetailsComponent"/> entity filter as the component emitter.
        /// </summary>
        internal static List<string> GetEnumNamebookListImports(List<SpiderlyClass> currentProjectEntities, List<SpiderlyClass> entities, List<SpiderlyClass> customDTOClasses)
        {
            return currentProjectEntities
                .Where(x => x.GeneratesDetailsComponent())
                .SelectMany(entity => NgDetailsPropertyBlockGenerator.GetEnumDropdownContexts(entity, entities, customDTOClasses))
                .Select(context => context.Property.Type.CoreName)
                .Distinct()
                .OrderBy(x => x)
                .Select(enumName => $$"""import { get{{enumName}}NamebookList } from '../enums/enums.generated';""")
                .ToList();
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

                result.Add($$"""
import { {{string.Join(", ", projectImports.DistinctBy(x => x.Name).Select(x => x.Name))}} } from '../entities/entities.generated';
""");
            }

            return result;
        }
    }
}
