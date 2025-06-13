import { Component, EventEmitter, Inject, Input, LOCALE_ID, OnInit, Output, ViewChild } from '@angular/core';
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
import { SelectChangeEvent, SelectModule } from 'primeng/select';

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
  @Input() items: any[]; // Pass only when hasLazyLoad === false
  @Input() rows: number = 10;
  @Input() cols: Column[];
  totalRecords: number;
  @Input() showCardWrapper: boolean = true;
  @Output() onLazyLoad: EventEmitter<TableFilter> = new EventEmitter();
  
  @Input() getTableDataObservableMethod: (tableFilter: TableFilter) => Observable<TableResponse>;

  lastLazyLoadEvent: TableLazyLoadEvent;
  loading: boolean = true;
  
  constructor(
    private router: Router,
    private dialogService: DialogService,
    private route: ActivatedRoute,
    private messageService: SpiderlyMessageService,
    private translocoService: TranslocoService,
    @Inject(LOCALE_ID) private locale: string
  ) {}

  ngOnInit(): void {

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

  t = false;
  filterText(event: Event){
    console.log(event)
    if (this.t == true) {
      this.table.filter(event, 'name', 1 as any)
      this.t = false;
    }
    else{
      this.table.filter(event, 'name', 2 as any)
      this.t = true;
    }

  }

  filterNumeric(event: Event){
    this.table.filter(event, 'id', 2 as any)
  }

  filterBoolean(event: SelectChangeEvent){
  }

  filterDate(event: Date){
  }
  
  getColMatchMode(filterType: string): any {
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

  test(event){
console.log(event)
  }

  clear(table: Table) {
    table.clear();
  }
}