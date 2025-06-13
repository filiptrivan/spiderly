import { Component, ContentChild, EventEmitter, Inject, Input, LOCALE_ID, OnInit, Output, TemplateRef, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { Table, TableFilterEvent, TableLazyLoadEvent, TableModule } from 'primeng/table';
import { DialogService } from 'primeng/dynamicdialog';
import { CommonModule, formatDate } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { Observable } from 'rxjs';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SpiderlyControlsModule } from '../../controls/spiderly-controls.module';
import { TableResponse } from '../../entities/table-response';
import { TableFilter } from '../../entities/table-filter';
import { TooltipModule } from 'primeng/tooltip';
import { ButtonModule } from 'primeng/button';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Action, Column } from '../spiderly-data-table/spiderly-data-table.component';
import { SelectItem } from 'primeng/api';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';

@Component({
    selector: 'spiderly-data-view',
    templateUrl: './spiderly-data-view.component.html',
    styleUrl: 'spiderly-data-view.component.scss',
    imports: [
        FormsModule,
        CommonModule,
        TranslocoDirective,
        SpiderlyControlsModule,
        TableModule,
        ButtonModule,
        MultiSelectModule,
        CheckboxModule,
        TooltipModule,
        DatePickerModule,
        InputTextModule,
        InputNumberModule,
        SelectModule,
    ]
})
export class SpiderlyDataViewComponent<T> implements OnInit {
  @ViewChild('dt') table: Table;
  @Input() items: T[]; // Pass only when hasLazyLoad === false
  @Input() rows: number = 10;
  @Input() cols: Column[];
  totalRecords: number;
  @Input() showCardWrapper: boolean = true;
  @Output() onLazyLoad: EventEmitter<TableFilter> = new EventEmitter();
  
  @Input() getTableDataObservableMethod: (tableFilter: TableFilter) => Observable<TableResponse>;

  lastLazyLoadEvent: TableLazyLoadEvent;
  loading: boolean = true;

  matchModeDateOptions: SelectItem[] = [];
  matchModeNumberOptions: SelectItem[] = [];
  
  @ContentChild('cardBody', { read: TemplateRef }) cardBody!: TemplateRef<any>;

  constructor(
    private router: Router,
    private route: ActivatedRoute,
    private translocoService: TranslocoService,
    @Inject(LOCALE_ID) private locale: string
  ) {}

  ngOnInit(): void {
    this.matchModeDateOptions = [
      { label: this.translocoService.translate('OnDate'), value: MatchModeCodes.Equals },
      { label: this.translocoService.translate('DatesBefore'), value: MatchModeCodes.LessThan },
      { label: this.translocoService.translate('DatesAfter'), value: MatchModeCodes.GreaterThan },
    ];

    this.matchModeNumberOptions = [
      { label: this.translocoService.translate('Equals'), value: MatchModeCodes.Equals },
      { label: this.translocoService.translate('LessThan'), value: MatchModeCodes.LessThan },
      { label: this.translocoService.translate('MoreThan'), value: MatchModeCodes.GreaterThan },
    ];
  }
  
  lazyLoad(event: TableLazyLoadEvent) {
    this.lastLazyLoadEvent = event;
    
    const transformedFilter: { [K in keyof T]?: { value: any; matchMode: MatchModeCodes }[] } = {};

    for (const key in event.filters) {
      const filterMeta = event.filters[key];

      if (Array.isArray(filterMeta)) {
        transformedFilter[key] = filterMeta;
      } 
      else {
        transformedFilter[key] = [{
          value: filterMeta.value,
          matchMode: filterMeta.matchMode
        }];
      }
    }

    let tableFilter = event as unknown as TableFilter<T>;

    tableFilter.filters = transformedFilter;

    this.onLazyLoad.next(tableFilter);
    
    this.getTableDataObservableMethod(tableFilter).subscribe({
      next: async (res) => { 
        this.items = res.data;
        this.totalRecords = res.totalRecords;

        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  filter(event: TableFilterEvent){
  }
  
  getDefaultMatchMode(filterType: string): any {
    switch (filterType) {
        case 'text':
          return MatchModeCodes.Contains;
        case 'date':
          return MatchModeCodes.Equals;
        case 'multiselect':
          return MatchModeCodes.In;
        case 'boolean':
          return MatchModeCodes.Equals;
        case 'numeric':
          return MatchModeCodes.Equals
        default:
          return null;
      }
  }

  getMatchModeOptions(filterType: string){
    switch (filterType) {
        case 'text':
          return [];
        case 'date':
          return this.matchModeDateOptions;
        case 'multiselect':
          return [];
        case 'boolean':
          return [];
        case 'numeric':
          return this.matchModeNumberOptions;
        default:
          return [];
      }
  }

  navigateToDetails(rowId: number){
    this.router.navigate([rowId], {relativeTo: this.route});
  }

  reload(){
    this.loading = true;
    this.items = null;
    this.lazyLoad(this.lastLazyLoadEvent);
  }

  colTrackByFn(index, item){
    return item.field;
  }

  actionTrackByFn(index, item: Action){
    return `${index}${item.field}`
  }

  applyFilters = () => {
    this.table._filter();
  }

  clearFilters() {
    this.table.clear();
  }
}

export interface DataViewCardBody<T> {
  $implicit: T;
  item: T;
  index: number;
}