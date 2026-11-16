using CaseConverter;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The Angular pages of one entity: its details page, and its list page as a table or a data view.
    /// <c>spiderly add-new-entity</c> writes them, and the seeded Role and User list pages reuse the table.
    /// </summary>
    internal static class EntityPagesTemplate
    {
        internal static string DetailsTs(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { HttpClient } from '@angular/common/http';
import { ChangeDetectorRef, Component, KeyValueDiffers, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ApiService } from 'src/app/business/services/api/api.service';
import { {{entityName}}MainUIForm, {{entityName}}SaveBody } from 'src/app/business/entities/entities.generated';
import { {{entityName}}BaseDetailsComponent } from 'src/app/business/components/base-details.generated';
import { BaseFormComponent, SpiderlyFormGroup, SpiderlyMessageService, BaseFormService, SpiderlyPanelsModule, SpiderlyControlsModule } from 'spiderly';

@Component({
    selector: '{{kebabEntityName}}-details',
    templateUrl: './{{kebabEntityName}}-details.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyPanelsModule,
        SpiderlyControlsModule,
        {{entityName}}BaseDetailsComponent
    ]
})
export class {{entityName}}DetailsComponent extends BaseFormComponent<{{entityName}}MainUIForm, {{entityName}}SaveBody> implements OnInit {
    override mainUIFormClass = {{entityName}}MainUIForm;
    override saveBodyClass = {{entityName}}SaveBody;

    constructor(
        protected override differs: KeyValueDiffers,
        protected override http: HttpClient,
        protected override messageService: SpiderlyMessageService, 
        protected override changeDetectorRef: ChangeDetectorRef,
        protected override router: Router, 
        protected override route: ActivatedRoute,
        protected override translocoService: TranslocoService,
        protected override baseFormService: BaseFormService,
        private apiService: ApiService,
    ) {
        super(differs, http, messageService, changeDetectorRef, router, route, translocoService, baseFormService);
    }

    override ngOnInit() {

    }

    override onBeforeSave = (): void => {

    }
}
""";
        }

        internal static string DetailsHtml(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
<ng-container *transloco="let t">

    <{{kebabEntityName}}-base-details
    [panelTitle]="t('{{entityName}}')"
    [parentFormGroup]="parentFormGroup" 
    (onSave)="onSave()"
    />

</ng-container>
""";
        }

        internal static string TableTs(string entityName)
        {
            return ListComponentTs(
                entityName,
                spiderlyImports: "Column, createFilterStore, numberFilter, SpiderlyDataTableComponent",
                filterEntries: """
        id: numberFilter({ label: t('Id') }),
""",
                colEntries: """
            {name: this.translocoService.translate('Id'), filterType: 'numeric', field: 'id'},
""");
        }

        /// <summary>
        /// The ONE list-component TS template — the generic entity scaffold and the seeded
        /// Role/User pages all emit through it, so a new table-surface feature (a store entry
        /// shape, a new input) lands in every scaffold with one edit. Nothing snapshots these
        /// templates; the e2e init pipeline compiling the emitted app is the only gate, which is
        /// exactly why three hand-kept near-copies kept drifting.
        /// </summary>
        internal static string ListComponentTs(string entityName, string spiderlyImports, string filterEntries, string colEntries)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { {{entityName}} } from 'src/app/business/entities/entities.generated';
import { {{spiderlyImports}} } from 'spiderly';

// The table's filter surface: one declared filter per filterable column. Declared apart from the
// component so the store's ids stay typed at every call site (views, programmatic writes).
function create{{entityName}}Filters(t: (key: string) => string) {
    return createFilterStore({
{{filterEntries.TrimEnd()}}
    });
}

@Component({
    selector: '{{kebabEntityName}}-list',
    templateUrl: './{{kebabEntityName}}-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyDataTableComponent,
    ]
})
export class {{entityName}}ListComponent implements OnInit {
    cols: Column<{{entityName}}>[];
    filters: ReturnType<typeof create{{entityName}}Filters>;

    getPaginated{{entityName}}ListObservableMethod = this.apiService.getPaginated{{entityName}}List;
    export{{entityName}}ListToExcelObservableMethod = this.apiService.export{{entityName}}ListToExcel;
    delete{{entityName}}ObservableMethod = this.apiService.delete{{entityName}};
    delete{{entityName}}ListObservableMethod = this.apiService.delete{{entityName}}List;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.filters = create{{entityName}}Filters((key) => this.translocoService.translate(key));
        this.cols = [
{{colEntries.TrimEnd()}}
            {actions:[
                {name: this.translocoService.translate('Details'), field: 'Details'},
                {name: this.translocoService.translate('Delete'), field: 'Delete'},
            ]},
        ]
    }
}
""";
        }

        internal static string TableHtml(string entityName)
        {
            return $$"""
<ng-container *transloco="let t">

    <spiderly-data-table [tableTitle]="t('{{entityName}}List')"
    [cols]="cols"
    [filters]="filters"
    [getPaginatedListObservableMethod]="getPaginated{{entityName}}ListObservableMethod"
    [exportListToExcelObservableMethod]="export{{entityName}}ListToExcelObservableMethod"
    [deleteItemFromTableObservableMethod]="delete{{entityName}}ObservableMethod"
    [deleteListFromTableObservableMethod]="delete{{entityName}}ListObservableMethod"
    [showAddButton]="true"
    ></spiderly-data-table>

</ng-container>
""";
        }

        internal static string DataViewTs(string entityName)
        {
            string kebabEntityName = entityName.ToKebabCase();

            return $$"""
import { ApiService } from 'src/app/business/services/api/api.service';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { Component, OnInit } from '@angular/core';
import { {{entityName}} } from 'src/app/business/entities/entities.generated';
import { DataViewCardBody, SpiderlyControlsModule, SpiderlyDataViewComponent, SpiderlyTemplateTypeDirective, DataViewFilter } from 'spiderly';

@Component({
    selector: '{{kebabEntityName}}-list',
    templateUrl: './{{kebabEntityName}}-list.component.html',
    imports: [
        TranslocoDirective,
        SpiderlyTemplateTypeDirective,
        SpiderlyDataViewComponent,
        SpiderlyControlsModule,
    ]
})
export class {{entityName}}ListComponent implements OnInit {
    templateType: DataViewCardBody<{{entityName}}>;
    filters: DataViewFilter<{{entityName}}>[];

    getPaginated{{entityName}}ListObservableMethod = this.apiService.getPaginated{{entityName}}List;

    constructor(
        private apiService: ApiService,
        private translocoService: TranslocoService,
    ) { }

    ngOnInit(){
        this.filters = [
            {label: this.translocoService.translate('Id'), type: 'numeric', field: 'id', showMatchModes: true},
        ]
    }
}
""";
        }

        internal static string DataViewHtml(string entityName)
        {
            return $$$"""
<ng-container *transloco="let t">

    <spiderly-data-view 
    [getPaginatedListObservableMethod]="getPaginated{{{entityName}}}ListObservableMethod" 
    [filters]="filters"
    >
        <ng-template #cardBody [templateType]="templateType" let-item let-index="index">
            <div class="card">
                {{item.id}}
            </div>
        </ng-template>
    </spiderly-data-view>

</ng-container>
""";
        }
    }
}
