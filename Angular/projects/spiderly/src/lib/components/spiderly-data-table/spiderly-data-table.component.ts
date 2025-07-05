import { Component, EventEmitter, Inject, Input, LOCALE_ID, OnInit, Output, ViewChild } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { SelectItem } from 'primeng/api';
import { Table, TableFilterEvent, TableLazyLoadEvent, TableModule } from 'primeng/table';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { SpiderlyDeleteConfirmationComponent } from '../spiderly-delete-dialog/spiderly-delete-confirmation.component';
import { CommonModule, formatDate } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { firstValueFrom, Observable } from 'rxjs';
import { PrimengOption } from '../../entities/primeng-option';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SpiderlyControlsModule } from '../../controls/spiderly-controls.module';
import { SpiderlyFormControl } from '../spiderly-form-control/spiderly-form-control';
import { PaginatedResult } from '../../entities/paginated-result';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { exportListToExcel, getHtmlImgDisplayString64 } from '../../services/helper-functions';
import { Filter } from '../../entities/filter';
import { TooltipModule } from 'primeng/tooltip';
import { ButtonModule } from 'primeng/button';
import { MultiSelectModule } from 'primeng/multiselect';
import { CheckboxModule } from 'primeng/checkbox';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';

@Component({
    selector: 'spiderly-data-table',
    templateUrl: './spiderly-data-table.component.html',
    styleUrl: 'spiderly-data-table.component.scss',
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
    ]
})
export class SpiderlyDataTableComponent implements OnInit {
  @ViewChild('dt') table: Table;
  @Input() tableTitle: string;
  @Input() tableIcon: string = 'pi pi-list';
  @Input() items: any[]; // Pass only when hasLazyLoad === false
  @Input() rows: number = 10;
  @Input() cols: Column[];
  @Input() showPaginator: boolean = true; // Pass only when hasLazyLoad === false
  @Input() showCardWrapper: boolean = false;
  @Input() readonly: boolean = false;
  totalRecords: number;
  @Output() onTotalRecordsChange: EventEmitter<number> = new EventEmitter();
  
  @Input() getPaginatedListObservableMethod: (tableFilter: Filter) => Observable<PaginatedResult>;
  @Input() exportListToExcelObservableMethod: (tableFilter: Filter) => Observable<any>;
  @Input() deleteItemFromTableObservableMethod: (rowId: number) => Observable<any>;

  lastLazyLoadEvent: TableLazyLoadEvent;
  loading: boolean = true;
  
  @Input() newlySelectedItems: number[] = [];
  fakeSelectedItems: number[] = []; // Only for showing checkboxes, we will not send this to the backend
  currentPageSelectedItemsFromDb: number[] = []; // Made so we can add only newly selected items to the newlySelectedItems
  @Input() unselectedItems: number[] = [];
  @Input() selectionMode: 'single' | 'multiple' | undefined | null;
  @Output() onLazyLoad: EventEmitter<Filter> = new EventEmitter();
  rowsSelectedNumber: number = 0;
  isAllSelected: boolean = null;
  fakeIsAllSelected: boolean = false; // Only for showing checkboxes, we will not send this to the backend
  isFirstTimeLazyLoad: boolean = true;
  @Output() onIsAllSelectedChange: EventEmitter<AllClickEvent> = new EventEmitter();
  @Input() selectedLazyLoadObservableMethod: (tableFilter: Filter) => Observable<LazyLoadSelectedIdsResult>;
  @Input() additionalFilterIdLong: number;
  
  matchModeDateOptions: SelectItem[] = [];
  matchModeNumberOptions: SelectItem[] = [];
  @Input() showAddButton: boolean = true; 
  @Input() showExportToExcelButton: boolean = true;
  @Input() showReloadTableButton: boolean = false;

  deleteRef: DynamicDialogRef;

  // Client side table
  // @Input() formArrayItems: any[]; // Pass this only if you have some additional logic for showing data
  @Input() getFormArrayItems: (additionalIndexes?: any) => any[];
  @Input() hasLazyLoad: boolean = true; 
  selectedItemIds: number[] = []; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  @Input() getAlreadySelectedItemIds: (additionalIndexes?: any) => number[]; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  selectedItems: any[] = []; // Pass only when hasLazyLoad === false
  @Input() getAlreadySelectedItems: (additionalIndexes?: any) => any[]; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  @Input() getFormControl: (formControlName: string, index: number, additionalIndexes?: any) => SpiderlyFormControl;
  @Input() additionalIndexes: any;
  @Output() onRowSelect: EventEmitter<RowClickEvent> = new EventEmitter();
  @Output() onRowUnselect: EventEmitter<RowClickEvent> = new EventEmitter();
    /**
   * if true, clicking a row will navigate to the details page.
   * Set to false to disable row navigation.
   * Default is false.
   */
  @Input()  navigateOnRowClick: boolean = false;

  /**
   * Path to navigate to when clicking a row.
   * If not provided, it will use the current route with the row ID.
   * Example: 'details' will navigate to '/details/{rowId}'.
   */
  @Input()  rowNavigationPath: string;

  constructor(
    private router: Router,
    private dialogService: DialogService,
    private route: ActivatedRoute,
    private messageService: SpiderlyMessageService,
    private translocoService: TranslocoService,
    @Inject(LOCALE_ID) private locale: string
  ) {}

  ngOnInit(): void {
    let a = new Filter<any>();
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

    if (this.hasLazyLoad === false) {
      this.clientLoad();
    }
  }
  
  lazyLoad(event: TableLazyLoadEvent) {
    this.lastLazyLoadEvent = event;

    let tableFilter: Filter = event as unknown as Filter;
    tableFilter.additionalFilterIdLong = this.additionalFilterIdLong;

    this.onLazyLoad.next(tableFilter);
    
    this.getPaginatedListObservableMethod(tableFilter).subscribe({
      next: async (res) => { 
        this.items = res.data;
        this.totalRecords = res.totalRecords;
        this.onTotalRecordsChange.next(res.totalRecords);
        
        if (this.selectedLazyLoadObservableMethod != null) {
          let selectedRowsMethodResult: LazyLoadSelectedIdsResult = await firstValueFrom(this.selectedLazyLoadObservableMethod(tableFilter));
  
          this.currentPageSelectedItemsFromDb = [...selectedRowsMethodResult.selectedIds];

          if (this.isFirstTimeLazyLoad == true) {
            this.rowsSelectedNumber = selectedRowsMethodResult.totalRecordsSelected;
            this.setFakeIsAllSelected();
            this.isFirstTimeLazyLoad = false;
          }
  
          if (this.isAllSelected == true) {
            let idsToInsert = [...this.items.map(x => x.id)];
            idsToInsert = idsToInsert.filter(x => this.unselectedItems.includes(x) == false);
            this.fakeSelectedItems = [...idsToInsert]; // Only for showing checkboxes, we will not send this to the backend
          }
          else if (this.isAllSelected == false) {
            this.fakeSelectedItems = [...this.newlySelectedItems]; // Only for showing checkboxes, we will not send this to the backend
          }
          else if (this.isAllSelected == null) {
            let idsToInsert = [...selectedRowsMethodResult.selectedIds, ...this.newlySelectedItems];
            idsToInsert = idsToInsert.filter(x => this.unselectedItems.includes(x) == false);
            this.fakeSelectedItems = [...idsToInsert];
          }
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  clientLoad(){
    this.loading = false;

    this.loadFormArrayItems();
    this.totalRecords = this.items.length;
    this.onTotalRecordsChange.next(this.items.length);

    if (this.getAlreadySelectedItemIds) {
      this.selectedItemIds = this.getAlreadySelectedItemIds(this.additionalIndexes);
    }
    if (this.getAlreadySelectedItems) {
      this.selectedItems = this.getAlreadySelectedItems(this.additionalIndexes);
    }
    this.rowsSelectedNumber = this.selectedItemIds.length;
    this.setFakeIsAllSelected();
  }

  private clientFilterCount = 0;

  filter(event: TableFilterEvent){
    if (this.hasLazyLoad && this.selectionMode === 'multiple')
      this.selectAll(false); // We need to do it like this because: totalRecords: 1 -> selectedRecords from earlyer selection 2 -> unselect current -> all checkbox is set to true

    if (this.hasLazyLoad === false && this.selectionMode === 'multiple') {
      if (this.clientFilterCount === 0) {
        this.loadFormArrayItems();
        this.clientFilterCount++;
      }else{
        this.clientFilterCount--;
      }
    }
  }

  private loadFormArrayItems(){
    this.items = this.getFormArrayItems(this.additionalIndexes);
    this.items.forEach((item, index) => {
      item.index = index;
    });
  }
  
  getColHeaderWidth(filterType: string) {
    switch (filterType) {
      case 'text':
        return 'min-width: 12rem;';
      case 'date':
        return 'min-width: 10rem;';
      case 'multiselect':
        return 'min-width: 12rem;';
      case 'boolean':
        return 'min-width: 8rem;';
      case 'numeric':
        return 'min-width: 12rem;';
      default:
        return 'width: 0rem;'; // fitting content of the row like this
    }
  }

  getColMatchModeOptions(filterType: string){
    switch (filterType) {
        case 'text':
          return null;
        case 'date':
          return this.matchModeDateOptions;
        case 'multiselect':
          return null;
        case 'boolean':
          return null;
        case 'numeric':
          return this.matchModeNumberOptions;
        default:
          return null;
      }
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

  isDropOrMulti(filterType: string){
    if (filterType == 'dropdown' || filterType == 'multiselect') {
        return true;
    } 
    else {
        return false;
    }
  }

/*
  * Navigate to details page based on rowId and rowNavigationPath.
  * If rowNavigationPath is provided, it will navigate to that path with the rowId.
  * If not, it will navigate to the current route with the rowId.
*/
navigateToDetails(rowId: number): void{
  if (!rowId) return;

  if (this.rowNavigationPath){
    const cleanPath = this.rowNavigationPath.replace(/^\/|\/$/g, ''); // Remove leading and trailing slashes

    this.router.navigateByUrl(`/${cleanPath}/${rowId}`);

  }
  else {

    this.router.navigate([rowId], { relativeTo: this.route });
  }
 }

  /* 
  * Handle row click event.
 */
 onRowClick(row: any): void {
  if (!this.navigateOnRowClick || !row?.id) return;
  this.navigateToDetails(row.id);
 }

  deleteObject(rowId: number){
    this.deleteRef = this.dialogService.open(SpiderlyDeleteConfirmationComponent, 
      { 
        header: this.translocoService.translate('AreYouSure'),
        width: '400px',
        data:{ deleteItemFromTableObservableMethod: this.deleteItemFromTableObservableMethod, id: rowId, } 
      });

      this.deleteRef.onClose.subscribe((deletedSuccessfully: boolean)=>{
        if(deletedSuccessfully === true){
          this.messageService.successMessage(this.translocoService.translate('SuccessfullyDeletedMessage'));
          this.reload();
        }
      });
  }

  reload(){
    this.loading = true;
    this.items = null;
    this.lazyLoad(this.lastLazyLoadEvent);
  }

  showActions(): boolean {
    return this.cols.some(x => x.actions?.length > 0);
  }

  getStyleForBodyColumn(col: Column<any>) {
    switch(col.filterType){
      case 'numeric':
        return 'text-align: right;';
      default:
        return null;
    }
  }

  getClassForAction(action: Action): string{
    switch(action.field){
      case 'Details':
        return 'pi pi-pencil text-lg cursor-pointer primary-color';
      case 'Delete':
        return 'pi pi-trash text-lg text-red-500 cursor-pointer';
      default:
        return `${action.icon} ${action.style} text-lg cursor-pointer`;
    }
  }

  getMethodForAction(action: Action, rowData: any){
    switch(action.field){
      case 'Details':
        return this.navigateToDetails(rowData.id);
      case 'Delete':
        return this.deleteObject(rowData.id);
      default:
        return action.onClick(rowData.id);
    }
  }

  getRowData(rowData: any, col: Column): string{
      switch (col.filterType) {
        case 'text':
          return rowData[col.field];
        case 'date':
          if (rowData[col.field] == null)
            return null;
          
          if (col.showTime)
            return formatDate(rowData[col.field], 'dd.MM.yyyy. HH:mm', this.locale);
          else
            return formatDate(rowData[col.field], 'dd.MM.yyyy.', this.locale);
        case 'multiselect':
          return rowData[col.field];
        case 'boolean':
          return rowData[col.field] == true ? this.translocoService.translate('Yes') : this.translocoService.translate('No');
        case 'numeric':
          // TODO: make decimal pipe
          return rowData[col.field];
        case 'blob':
          return getHtmlImgDisplayString64(rowData[col.field]);
        default:
          return null;
      }
  }

  colTrackByFn(index, item){
    return item.field;
  }

  actionTrackByFn(index, item: Action){
    return `${index}${item.field}`
  }

  exportListToExcel() {
    let filter: Filter = this.lastLazyLoadEvent as unknown as Filter;
    filter.additionalFilterIdLong = this.additionalFilterIdLong;

    exportListToExcel(this.exportListToExcelObservableMethod, filter);
  }

  clear(table: Table) {
    table.clear();
  }

  //#region Selection

  setFakeIsAllSelected(){
    if(this.rowsSelectedNumber == this.totalRecords)
      this.fakeIsAllSelected = true;
    else
      this.fakeIsAllSelected = false;
  }

  selectAll(checked: boolean){
    this.unselectedItems.length = 0;
    this.newlySelectedItems.length = 0;

    if (checked == true) {
      this.isAllSelected = true;
      this.fakeIsAllSelected = true;
      this.onIsAllSelectedChange.next(new AllClickEvent({checked: true, additionalIndexes: this.additionalIndexes}));
      this.rowsSelectedNumber = this.totalRecords;
      this.fakeSelectedItems = [...this.items.map(x => x.id)];
      this.selectedItemIds = [...this.items.map(x => x.id)]
    }
    else{
      this.isAllSelected = false;
      this.fakeIsAllSelected = false;
      this.onIsAllSelectedChange.next(new AllClickEvent({checked: false, additionalIndexes: this.additionalIndexes}));
      this.rowsSelectedNumber = 0;
      this.fakeSelectedItems = [];
      this.selectedItemIds = [];
    }
  }

  selectRow(id: number, index: number) {
    if (this.isRowSelected(id)) {
      this.rowUnselect(id);
      this.onRowUnselect.next(new RowClickEvent({ index: index, id: id, additionalIndexes: this.additionalIndexes }));
    } else {
      this.rowSelect(id);
      this.onRowSelect.next(new RowClickEvent({ index: index, id: id, additionalIndexes: this.additionalIndexes }));
    }
  }

  isRowSelected(id: number){
    if (this.hasLazyLoad){
      return this.fakeSelectedItems.find(x => x === id) != undefined;
    }
    else {
      return this.selectedItemIds.find(x => x === id) != undefined;
    }
  }

  rowSelect(id: number){
    if (this.isAllSelected == false || this.currentPageSelectedItemsFromDb.includes(id) == false) {
      this.newlySelectedItems.push(id);
    }

    if (this.hasLazyLoad){
      this.fakeSelectedItems.push(id);
    }
    else {
      this.selectedItemIds.push(id);
    }
    
    this.rowsSelectedNumber++;

    const index = this.unselectedItems.indexOf(id);
    if (index !== -1) {
      this.unselectedItems.splice(index, 1); // Splice is mutating the array
    }

    this.setFakeIsAllSelected();
  }
  
  rowUnselect(id: number) {
    if (this.isAllSelected == true || this.currentPageSelectedItemsFromDb.includes(id) == true) {
      this.unselectedItems.push(id);
    }

    this.rowsSelectedNumber--;

    const index = this.newlySelectedItems.indexOf(id);
    const fakeIndex = this.fakeSelectedItems.indexOf(id);
    const nonLazyLoadIndex = this.selectedItemIds.indexOf(id);

    if (index !== -1) {
      this.newlySelectedItems.splice(index, 1); // Splice is mutating the array
    }
    if (fakeIndex !== -1) {
      this.fakeSelectedItems.splice(fakeIndex, 1); // Splice is mutating the array
    }
    if (nonLazyLoadIndex !== -1) {
      this.selectedItemIds.splice(nonLazyLoadIndex, 1); // Splice is mutating the array
    }

    this.setFakeIsAllSelected();
  }
  //#endregion

  //#region Client side table

  // Can do it with Id also, because we are never adding the new record in the table at the same page.
  getFormArrayControlByIndex(formControlName: string, index: number): SpiderlyFormControl{
    if (this.getFormControl) {
      return this.getFormControl(formControlName, index, this.additionalIndexes);
    }
    else{
      return null;
    }
  }

  //#endregion
}

export class Action {
  name?: string;
  field?: string;
  icon?: string;
  style?: string;
  onClick?: (id: number) => void;

  constructor(
    {
      name,
      field,
      icon,
      style,
      onClick,
    }:{
      name?: string;
      field?: string;
      icon?: string;
      style?: string;
      onClick?: () => void;
    } = {}
    ) {
      this.name = name;
      this.field = field;
      this.icon = icon;
      this.style = style;
      this.onClick = onClick;
  }
}

export class Column<T = any> {
  name?: string;
  field?: string & keyof T;
  filterField?: string & keyof T; // Made specificaly for multiautocomplete, maybe for something more in the future
  filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
  filterPlaceholder?: string;
  showMatchModes?: boolean;
  showAddButton?: boolean;
  dropdownOrMultiselectValues?: PrimengOption[];
  actions?: Action[];
  editable?: boolean;
  showTime?: boolean;

  constructor(
    {
      name,
      field,
      filterField,
      filterType,
      filterPlaceholder,
      showMatchModes,
      showAddButton,
      dropdownOrMultiselectValues,
      actions,
      editable,
      showTime,
    }:{
      name?: string;
      field?: string & keyof T;
      filterField?: string & keyof T; // Made specificaly for multiautocomplete, maybe for something more in the future;
      filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric';
      filterPlaceholder?: string;
      showMatchModes?: boolean;
      showAddButton?: boolean;
      dropdownOrMultiselectValues?: PrimengOption[];
      actions?: Action[];
      editable?: boolean;
      showTime?: boolean;
    } = {}
    ) {
      this.name = name;
      this.field = field;
      this.filterField = filterField;
      this.filterType = filterType;
      this.filterPlaceholder = filterPlaceholder;
      this.showMatchModes = showMatchModes;
      this.showAddButton = showAddButton;
      this.dropdownOrMultiselectValues = dropdownOrMultiselectValues;
      this.actions = actions;
      this.editable = editable;
      this.showTime = showTime;
  }
}

export class RowClickEvent {
  index?: number;
  id?: number;
  additionalIndexes?: any;

  constructor(
    {
      index, 
      id, 
      additionalIndexes
    }:{
      index?: number; 
      id?: number; 
      additionalIndexes?: any;
    } = {}
    ) {
    this.index = index;
    this.id = id;
    this.additionalIndexes = additionalIndexes;
  }
}

export class AllClickEvent {
  checked?: boolean;
  additionalIndexes?: any;

  constructor(
    {
      checked, 
      additionalIndexes
    }:{
      checked?: boolean; 
      additionalIndexes?: any;
    } = {}
    ) {
    this.checked = checked;
    this.additionalIndexes = additionalIndexes;
  }
}