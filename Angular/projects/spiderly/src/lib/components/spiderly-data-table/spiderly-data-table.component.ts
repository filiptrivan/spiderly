import { CommonModule, formatDate, formatNumber } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ContentChild,
  EventEmitter,
  Inject,
  Input,
  LOCALE_ID,
  OnDestroy,
  OnInit,
  Output,
  TemplateRef,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SelectItem, SortMeta } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { MultiSelectModule } from 'primeng/multiselect';
import { PopoverModule } from 'primeng/popover';
import {
  Table,
  TableFilterEvent,
  TableLazyLoadEvent,
  TableModule,
} from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { SpiderlyControlsModule } from '../../controls/spiderly-controls.module';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { Filter } from '../../entities/filter';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { PaginatedResult } from '../../entities/paginated-result';
import { PrimengOption } from '../../entities/primeng-option';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { ConfigServiceBase } from '../../services/config.service.base';
import {
  exportListToExcel,
  getHtmlImgDisplayString64,
  parseDateOnlyLocal,
} from '../../services/helper-functions';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { readStoredJson } from '../../services/web-storage';
import {
  DeleteConfirmationData,
  SpiderlyDeleteConfirmationComponent,
} from '../spiderly-delete-dialog/spiderly-delete-confirmation.component';
import { SpiderlyFormControl } from '../spiderly-form-control/spiderly-form-control';

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
    PopoverModule,
    DatePickerModule,
    CheckboxModule,
    TooltipModule,
  ],
})
export class SpiderlyDataTableComponent
  implements OnInit, AfterViewInit, OnDestroy
{
  private readonly destroy$ = new Subject<void>();

  @ViewChild('dt') table: Table;

  /**
   * Custom toolbar content projected via `<ng-template spiderlyDataTableActions>`.
   * Rendered in the caption action area ahead of the built-in buttons.
   */
  @ContentChild(SpiderlyDataTableActionsDirective, { read: TemplateRef })
  actionsTemplate: TemplateRef<any>;

  @Input() tableTitle: string;
  @Input() tableIcon: string = 'pi pi-list';
  @Input() items: any[]; // Pass only when hasLazyLoad === false
  @Input() rows: number;
  @Input() cols: Column[];
  /** Whether the paginator is shown. Pass only when `hasLazyLoad === false`. Defaults to `true`. */
  @Input() showPaginator: boolean = true;
  /** Whether the table is wrapped in a card container. Defaults to `false`. */
  @Input() showCardWrapper: boolean = false;
  @Input() readonly: boolean = false;
  @Input() idField = 'id';
  totalRecords: number;
  @Output() onTotalRecordsChange: EventEmitter<number> = new EventEmitter();

  @Input() getPaginatedListObservableMethod: (
    tableFilter: Filter,
  ) => Observable<PaginatedResult>;
  @Input() exportListToExcelObservableMethod: (
    tableFilter: Filter,
  ) => Observable<any>;
  @Input() deleteItemFromTableObservableMethod: (
    rowId: number,
  ) => Observable<any>;
  @Input() deleteListFromTableObservableMethod: (
    ids: number[],
  ) => Observable<any>;

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
  @Output() onIsAllSelectedChange: EventEmitter<AllClickEvent> =
    new EventEmitter();
  @Input() selectedLazyLoadObservableMethod: (
    tableFilter: Filter,
  ) => Observable<LazyLoadSelectedIdsResult>;
  @Input() additionalFilterIdLong: number;

  matchModeDateOptions: SelectItem[] = [];
  matchModeNumberOptions: SelectItem[] = [];
  /** Whether the "Add" button is shown. Defaults to `true`. */
  @Input() showAddButton: boolean = true;
  /** Whether the "Export to Excel" button is shown. Defaults to `true`. */
  @Input() showExportToExcelButton: boolean = true;
  /** Whether the reload-table button is shown. Defaults to `false`. */
  @Input() showReloadTableButton: boolean = false;

  deleteRef: DynamicDialogRef;

  // Client side table
  // @Input() formArrayItems: any[]; // Pass this only if you have some additional logic for showing data
  @Input() getFormArrayItems: (additionalIndexes?: any) => any[];
  @Input() hasLazyLoad: boolean = true;

  /**
   * Unique key for persisting table state (filters, sort, pagination) to storage.
   * Auto-generated from the current route when not provided.
   */
  @Input() stateKey?: string;

  /** 'session' persists across refresh only; 'local' persists indefinitely. */
  @Input() stateStorage: 'session' | 'local' = 'session';

  /**
   * Column field the table sorts by while the user has no sort of their own — applied on
   * first load (persisted state, when present, wins) and re-applied whenever an action
   * would leave the table unsorted (tri-state header click, Clear filters). Shows as a
   * regular header sort arrow. Omit to keep the backend's implicit Id DESC ordering.
   */
  @Input() defaultSortField?: string;

  /** Direction for `defaultSortField`: 1 ascending (default), -1 descending. */
  @Input() defaultSortOrder: 1 | -1 = 1;

  initialMultiSortMeta: SortMeta[] | null = null;

  /** Columns currently rendered — `cols` minus the hidden ones. Actions columns always render. */
  visibleCols: Column[] = [];

  /** Cached count of visible data columns, so template bindings don't re-derive it per CD cycle. */
  private visibleDataColsCount = 0;

  /** Data columns offered in the column chooser (actions columns are excluded). */
  chooserCols: Column[] = [];

  resolvedStateKey: string | null = null;
  selectedItemIds: number[] = []; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  @Input() getAlreadySelectedItemIds: (additionalIndexes?: any) => number[]; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  selectedItems: any[] = []; // Pass only when hasLazyLoad === false
  @Input() getAlreadySelectedItems: (additionalIndexes?: any) => any[]; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
  @Input() getFormControl: (
    formControlName: string,
    index: number,
    additionalIndexes?: any,
  ) => SpiderlyFormControl;
  @Input() additionalIndexes: any;
  @Output() onRowSelect: EventEmitter<RowClickEvent> = new EventEmitter();
  @Output() onRowUnselect: EventEmitter<RowClickEvent> = new EventEmitter();
  /**
   * if true, clicking a row will navigate to the details page.
   * Set to false to disable row navigation.
   * Default is false.
   */
  @Input() navigateOnRowClick: boolean = false;

  /**
   * Path to navigate to when clicking a row.
   * If not provided, it will use the current route with the row ID.
   * Example: 'details' will navigate to '/details/{rowId}'.
   */
  @Input() rowNavigationPath: string;

  constructor(
    private router: Router,
    private dialogService: DialogService,
    private route: ActivatedRoute,
    private messageService: SpiderlyMessageService,
    private translocoService: TranslocoService,
    private configService: ConfigServiceBase,
    @Inject(LOCALE_ID) private locale: string,
  ) {}

  ngAfterViewInit(): void {
    this.setupRemovableSort();
  }

  // PrimeNG v19 removed the removableSort property. This overrides the table's
  // sort() method to add tri-state cycling: ascending → descending → unsorted
  // (or back to the declared default sort when one is set).
  private setupRemovableSort(): void {
    const originalSort = this.table.sort.bind(this.table);

    this.table.sort = (event: { originalEvent: Event; field: string }) => {
      const sortMeta = this.table.getSortMeta(event.field);

      if (sortMeta && sortMeta.order === -1) {
        const mouseEvent = event.originalEvent as MouseEvent;
        const isMultiSortClick = mouseEvent.metaKey || mouseEvent.ctrlKey;

        // Un-sorting substitutes the declared default (when there is one) right here,
        // so sortMultiple() broadcasts and emits the final meta in one shot.
        if (isMultiSortClick) {
          const remaining = this.table._multiSortMeta.filter(
            (m) => m.field !== event.field,
          );
          this.table._multiSortMeta = remaining.length
            ? remaining
            : (this.defaultMultiSortMeta() ?? remaining);
        } else {
          this.table._multiSortMeta = this.defaultMultiSortMeta() ?? [];
          if (this.table.resetPageOnSort) {
            this.table._first = 0;
            this.table.firstChange.emit(0);
          }
        }

        this.table.sortMultiple();

        if (this.table.isStateful()) {
          this.table.saveState();
        }

        this.table.anchorRowIndex = null;
        return;
      }

      originalSort(event);
    };
  }

  ngOnInit(): void {
    if (this.rows == null) this.rows = this.configService.defaultPageSize;

    if (this.deleteListFromTableObservableMethod && !this.selectionMode) {
      this.selectionMode = 'multiple';
    }

    this.matchModeDateOptions = [
      {
        label: this.translocoService.translate('OnDate'),
        value: MatchModeCodes.Equals,
      },
      {
        label: this.translocoService.translate('DatesBefore'),
        value: MatchModeCodes.LessThan,
      },
      {
        label: this.translocoService.translate('DatesAfter'),
        value: MatchModeCodes.GreaterThan,
      },
    ];

    this.matchModeNumberOptions = [
      {
        label: this.translocoService.translate('Equals'),
        value: MatchModeCodes.Equals,
      },
      {
        label: this.translocoService.translate('LessThan'),
        value: MatchModeCodes.LessThan,
      },
      {
        label: this.translocoService.translate('MoreThan'),
        value: MatchModeCodes.GreaterThan,
      },
    ];

    if (this.hasLazyLoad) {
      const baseKey = this.stateKey ?? `spiderly-table:${this.router.url}`;
      this.resolvedStateKey =
        this.additionalFilterIdLong != null
          ? `${baseKey}:${this.additionalFilterIdLong}`
          : baseKey;
    } else {
      this.clientLoad();
    }

    this.restoreColumnVisibility();
    this.reconcileVisibilityWithPersistedConstraints();
    this.chooserCols = this.cols.filter(SpiderlyDataTableComponent.isDataColumn);
    this.refreshVisibleCols();

    // Bound to p-table's [multiSortMeta] so the FIRST request already carries the right
    // sort. Persisted user sort wins over the declared default; we read it ourselves
    // because PrimeNG's restoreState() runs on the first [value] change — after the
    // initial lazy emit has already left.
    this.initialMultiSortMeta =
      this.persistedMultiSortMeta() ?? this.defaultMultiSortMeta();
  }

  //#region Column visibility

  /**
   * User visibility choices (field → visible), holding only fields the user explicitly
   * toggled away from their declared default — so a page changing a column's declared
   * default later flows through to every user who never touched that column.
   */
  private columnVisibilityOverrides: Record<string, boolean> = {};

  /**
   * Columns revealed by the load-time reconciliation. Kept apart from
   * `columnVisibilityOverrides` so persisting a later unrelated toggle can't promote a
   * transient safety reveal into a durable choice.
   */
  private revealedByConstraint = new Set<string>();

  /** A data column shows values for a `field`; anything else (the actions column) always renders. */
  private static isDataColumn(col: Column): boolean {
    return !!col.field;
  }

  /**
   * Storage slot for the visibility overrides. Deliberately ALWAYS localStorage, even when
   * `stateStorage` is 'session': the column layout is a durable preference, while filters
   * are a transient working set — different natural lifetimes.
   */
  private get columnsStateKey(): string | null {
    return this.resolvedStateKey ? `${this.resolvedStateKey}:columns` : null;
  }

  /** Actions columns always render; data columns follow reveal/override, then declared default. */
  isColumnVisible(col: Column): boolean {
    if (!SpiderlyDataTableComponent.isDataColumn(col)) return true;
    if (col.lockVisible) return true; // pinned — wins over any (possibly stale) override
    if (this.revealedByConstraint.has(col.field)) return true;
    return this.columnVisibilityOverrides[col.field] ?? col.visible !== false;
  }

  private refreshVisibleCols(): void {
    this.visibleCols = this.cols.filter((col) => this.isColumnVisible(col));
    this.visibleDataColsCount = this.visibleCols.filter(
      SpiderlyDataTableComponent.isDataColumn,
    ).length;
  }

  /**
   * Locked columns can't be toggled; the last visible data column can't be hidden — the
   * table must never collapse to just an actions column. Revealing is always allowed.
   */
  canToggleColumn(col: Column): boolean {
    if (col.lockVisible) return false;
    if (!this.isColumnVisible(col)) return true;
    return this.visibleDataColsCount > 1;
  }

  toggleColumn(col: Column, visible: boolean): void {
    this.revealedByConstraint.delete(col.field); // an explicit choice supersedes a safety reveal

    if (visible === (col.visible !== false)) {
      delete this.columnVisibilityOverrides[col.field]; // back at the declared default
    } else {
      this.columnVisibilityOverrides[col.field] = visible;
    }

    this.persistColumnVisibility();
    this.refreshVisibleCols();

    if (!visible) this.clearHiddenColumnConstraints([col]);
  }

  /** Restores every column to its declared default and forgets the stored override. */
  resetColumnVisibility(): void {
    const wasVisible = this.visibleCols.filter(
      SpiderlyDataTableComponent.isDataColumn,
    );

    this.revealedByConstraint.clear();
    this.columnVisibilityOverrides = {};
    this.persistColumnVisibility();
    this.refreshVisibleCols();

    // Columns the reset just hid follow the same rule as a manual hide.
    this.clearHiddenColumnConstraints(
      wasVisible.filter((col) => !this.isColumnVisible(col)),
    );
  }

  /**
   * A hidden column contributes nothing to filtering or sorting: the header is the only
   * filter surface, so a kept constraint would restrict the data invisibly. Reloads (once)
   * only when a constraint was actually cleared — plain hides don't need a server round-trip.
   */
  private clearHiddenColumnConstraints(cols: Column[]): void {
    if (!this.table) return;

    let cleared = false;
    for (const col of cols) {
      const filterKey = col.filterField ?? col.field;
      if (
        SpiderlyDataTableComponent.isActiveFilterMeta(
          this.table.filters?.[filterKey],
        )
      ) {
        delete this.table.filters[filterKey];
        cleared = true;
      }

      if (this.table._multiSortMeta?.some((m) => m.field === col.field)) {
        this.table._multiSortMeta = this.table._multiSortMeta.filter(
          (m) => m.field !== col.field,
        );
        this.table.tableService.onSort(this.table._multiSortMeta);
        cleared = true;
      }
    }

    if (cleared) this.table._filter(); // re-emits the lazy load and saves the cleaned state
  }

  /** Whether filter metadata (single or array) carries a real constraint, not just an empty slot. */
  private static isActiveFilterMeta(meta: unknown): boolean {
    return (Array.isArray(meta) ? meta : [meta]).some(
      (m: any) =>
        m?.value != null &&
        m.value !== '' &&
        (!Array.isArray(m.value) || m.value.length > 0),
    );
  }

  private persistColumnVisibility(): void {
    if (!this.columnsStateKey) return;

    if (Object.keys(this.columnVisibilityOverrides).length === 0) {
      localStorage.removeItem(this.columnsStateKey);
    } else {
      localStorage.setItem(
        this.columnsStateKey,
        JSON.stringify(this.columnVisibilityOverrides),
      );
    }
  }

  private restoreColumnVisibility(): void {
    if (!this.columnsStateKey) return;

    this.columnVisibilityOverrides =
      readStoredJson(localStorage, this.columnsStateKey) ?? {};
  }

  /**
   * PrimeNG's persisted table state (filters/sort/pagination), read directly from storage
   * because we need it before PrimeNG's restoreState() runs on the first [value] change.
   */
  private persistedTableState(): any {
    if (!this.resolvedStateKey) return null;

    return readStoredJson(
      this.stateStorage === 'local' ? localStorage : sessionStorage,
      this.resolvedStateKey,
    );
  }

  /**
   * Guards the "hidden contributes nothing" invariant against state this component didn't
   * write (older blobs, hand-edited storage): a hidden column the persisted table state
   * still filters or sorts by is revealed rather than constraining the data invisibly.
   * In-memory only — once the constraint is gone, the user's stored choice reapplies.
   */
  private reconcileVisibilityWithPersistedConstraints(): void {
    const state = this.persistedTableState();
    if (!state) return;

    const constrained = new Set<string>();
    for (const [field, meta] of Object.entries(state.filters ?? {})) {
      if (SpiderlyDataTableComponent.isActiveFilterMeta(meta))
        constrained.add(field);
    }
    for (const sortMeta of state.multiSortMeta ?? []) {
      if (sortMeta?.field) constrained.add(sortMeta.field);
    }

    for (const col of this.cols) {
      if (!SpiderlyDataTableComponent.isDataColumn(col)) continue;
      if (this.isColumnVisible(col)) continue;
      if (constrained.has(col.field) || constrained.has(col.filterField)) {
        this.revealedByConstraint.add(col.field);
      }
    }
  }

  //#endregion

  /**
   * The declared default as PrimeNG sort meta, or null when none is declared — or when its
   * column is hidden ("hidden contributes nothing"): the table then falls back to the
   * backend's implicit Id DESC, exactly as if no default were declared.
   */
  private defaultMultiSortMeta(): SortMeta[] | null {
    if (!this.defaultSortField) return null;

    const defaultSortCol = this.cols?.find(
      (col) => col.field === this.defaultSortField,
    );
    if (defaultSortCol && !this.isColumnVisible(defaultSortCol)) return null;

    return [{ field: this.defaultSortField, order: this.defaultSortOrder }];
  }

  private persistedMultiSortMeta(): SortMeta[] | null {
    const state = this.persistedTableState();
    return state?.multiSortMeta?.length ? state.multiSortMeta : null;
  }

  // Safety net: any lazy load still leaving unsorted (Clear filters — PrimeNG's clear()
  // nulls the sort and emits in one call — or stale persisted state) falls back to the
  // declared default. Patching the event before it becomes the backend Filter avoids a
  // second fetch; clear() broadcasts sort state to the header icons before emitting,
  // hence the explicit re-broadcast. The tri-state un-sort path never gets here — it
  // substitutes the default at its source in setupRemovableSort().
  private applyDefaultSortIfUnsorted(event: TableLazyLoadEvent): void {
    if (event.multiSortMeta?.length) return;

    const defaultSort = this.defaultMultiSortMeta();
    if (defaultSort == null) return;

    event.multiSortMeta = defaultSort;

    if (this.table) {
      this.table._multiSortMeta = defaultSort;
      this.table.tableService.onSort(defaultSort);
    }
  }

  lazyLoad(event: TableLazyLoadEvent) {
    this.applyDefaultSortIfUnsorted(event);
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
          let selectedRowsMethodResult: LazyLoadSelectedIdsResult =
            await firstValueFrom(
              this.selectedLazyLoadObservableMethod(tableFilter),
            );

          this.currentPageSelectedItemsFromDb = [
            ...selectedRowsMethodResult.selectedIds,
          ];

          if (this.isFirstTimeLazyLoad == true) {
            this.rowsSelectedNumber =
              selectedRowsMethodResult.totalRecordsSelected;
            this.setFakeIsAllSelected();
            this.isFirstTimeLazyLoad = false;
          }

          if (this.isAllSelected == true) {
            let idsToInsert = [...this.items.map((x) => x[this.idField])];
            idsToInsert = idsToInsert.filter(
              (x) => this.unselectedItems.includes(x) == false,
            );
            this.fakeSelectedItems = [...idsToInsert]; // Only for showing checkboxes, we will not send this to the backend
          } else if (this.isAllSelected == false) {
            this.fakeSelectedItems = [...this.newlySelectedItems]; // Only for showing checkboxes, we will not send this to the backend
          } else if (this.isAllSelected == null) {
            let idsToInsert = [
              ...selectedRowsMethodResult.selectedIds,
              ...this.newlySelectedItems,
            ];
            idsToInsert = idsToInsert.filter(
              (x) => this.unselectedItems.includes(x) == false,
            );
            this.fakeSelectedItems = [...idsToInsert];
          }
        }

        if (this.selectedLazyLoadObservableMethod == null && this.deleteListFromTableObservableMethod) {
          this.fakeSelectedItems = this.items
            .map((x) => x[this.idField])
            .filter((id) => this.newlySelectedItems.includes(id));
        }

        this.loading = false;
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  clientLoad() {
    this.loading = false;

    this.loadFormArrayItems();
    this.totalRecords = this.items.length;
    this.onTotalRecordsChange.next(this.items.length);

    if (this.getAlreadySelectedItemIds) {
      this.selectedItemIds = this.getAlreadySelectedItemIds(
        this.additionalIndexes,
      );
    }
    if (this.getAlreadySelectedItems) {
      this.selectedItems = this.getAlreadySelectedItems(this.additionalIndexes);
    }
    this.rowsSelectedNumber = this.selectedItemIds.length;
    this.setFakeIsAllSelected();
  }

  private clientFilterCount = 0;

  filter(event: TableFilterEvent) {
    if (this.hasLazyLoad && this.selectionMode === 'multiple')
      this.selectAll(false); // We need to do it like this because: totalRecords: 1 -> selectedRecords from earlyer selection 2 -> unselect current -> all checkbox is set to true

    if (this.hasLazyLoad === false && this.selectionMode === 'multiple') {
      if (this.clientFilterCount === 0) {
        this.loadFormArrayItems();
        this.clientFilterCount++;
      } else {
        this.clientFilterCount--;
      }
    }
  }

  private loadFormArrayItems() {
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

  getColMatchModeOptions(filterType: string) {
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
        return MatchModeCodes.Equals;
      default:
        return null;
    }
  }

  isDropOrMulti(filterType: string) {
    if (filterType == 'dropdown' || filterType == 'multiselect') {
      return true;
    } else {
      return false;
    }
  }

  /*
   * Navigate to details page based on rowId and rowNavigationPath.
   * If rowNavigationPath is provided, it will navigate to that path with the rowId.
   * If not, it will navigate to the current route with the rowId.
   */
  navigateToDetails(rowId: number): void {
    if (rowId == null) return;

    if (this.rowNavigationPath) {
      const cleanPath = this.rowNavigationPath.replace(/^\/|\/$/g, ''); // Remove leading and trailing slashes

      this.router.navigateByUrl(`/${cleanPath}/${rowId}`);
    } else {
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

  /*
   * Handle a cell click. Only columns that opt in (have an `onCellClick` callback) react; for them
   * we stop propagation so the click does not also trigger row navigation, then invoke the consumer
   * callback with a fully-populated CellClickEvent. Inert cells fall through to onRowClick unchanged.
   */
  onCellClick(col: Column, rowData: any, event: MouseEvent): void {
    if (!col.onCellClick) return;
    event.stopPropagation();
    col.onCellClick({
      ...this.buildClickEvent(rowData, event),
      field: col.field,
      value: rowData[col.field],
      displayValue: this.getRowData(rowData, col),
    });
  }

  /*
   * Build the base click payload shared by custom action clicks and cell clicks. The clicked element
   * is captured here, not read later from `originalEvent.currentTarget` (which nulls after dispatch).
   */
  private buildClickEvent(rowData: any, event: MouseEvent): ActionClickEvent {
    return {
      id: rowData[this.idField],
      row: rowData,
      element: event.currentTarget as HTMLElement,
      originalEvent: event,
    };
  }

  deleteObject(rowId: number) {
    this.openDeleteConfirmation(
      {
        deleteItemFromTableObservableMethod:
          this.deleteItemFromTableObservableMethod,
        id: rowId,
        message: this.translocoService.translate('PleaseConfirmToProceed'),
      },
      'SuccessfullyDeletedMessage',
    );
  }

  deleteSelectedObjects() {
    const selectedIds = [...this.newlySelectedItems];

    if (selectedIds.length === 0) return;

    this.openDeleteConfirmation(
      {
        deleteListFromTableObservableMethod:
          this.deleteListFromTableObservableMethod,
        ids: selectedIds,
        message: this.translocoService.translate('DeleteBulkConfirmation', {
          count: selectedIds.length,
        }),
      },
      'SuccessfullyDeletedListMessage',
      () => this.resetSelection(),
    );
  }

  private openDeleteConfirmation(
    data: DeleteConfirmationData,
    successMessageKey: string,
    onSuccess?: () => void,
  ) {
    this.deleteRef = this.dialogService.open(
      SpiderlyDeleteConfirmationComponent,
      {
        header: this.translocoService.translate('AreYouSure'),
        width: '400px',
        data,
      },
    );

    this.deleteRef.onClose
      .pipe(takeUntil(this.destroy$))
      .subscribe((deletedSuccessfully: boolean) => {
        if (deletedSuccessfully === true) {
          this.messageService.successMessage(
            this.translocoService.translate(successMessageKey),
          );
          onSuccess?.();
          this.reload();
        }
      });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  get showSelectAllCheckbox(): boolean {
    return (
      this.selectionMode === 'multiple' &&
      this.selectedLazyLoadObservableMethod != null
    );
  }

  private resetSelection() {
    this.newlySelectedItems.length = 0;
    this.unselectedItems.length = 0;
    this.fakeSelectedItems = [];
    this.rowsSelectedNumber = 0;
    this.isAllSelected = null;
    this.fakeIsAllSelected = false;
  }

  reload() {
    this.loading = true;
    this.items = null;
    this.lazyLoad(this.lastLazyLoadEvent);
  }

  showActions(): boolean {
    return this.cols.some((x) => x.actions?.length > 0);
  }

  getStyleForBodyColumn(col: Column<any>) {
    switch (col.filterType) {
      case 'numeric':
        return 'text-align: right;';
      default:
        return null;
    }
  }

  getClassForAction(action: Action): string {
    switch (action.field) {
      case 'Details':
        return 'pi pi-pencil';
      case 'Delete':
        return 'pi pi-trash';
      default:
        return `${action.icon} ${action.styleClass}`;
    }
  }

  getStyleForAction(action: Action): string {
    switch (action.field) {
      case 'Delete':
        return 'cursor: pointer; color: var(--p-button-danger-background);';
      default:
        return `cursor: pointer; ${action.style}`;
    }
  }

  getMethodForAction(action: Action, rowData: any, event: MouseEvent) {
    switch (action.field) {
      case 'Details':
        return this.navigateToDetails(rowData[this.idField]);
      case 'Delete':
        return this.deleteObject(rowData[this.idField]);
      default:
        return action.onClick(this.buildClickEvent(rowData, event));
    }
  }

  getRowData(rowData: any, col: Column): string {
    switch (col.filterType) {
      case 'text':
        return rowData[col.field];
      case 'date':
        if (rowData[col.field] == null) return null;

        if (col.showTime)
          return formatDate(rowData[col.field], 'short', this.locale);

        const raw = rowData[col.field];
        const local = typeof raw === 'string' ? parseDateOnlyLocal(raw) : null;
        return formatDate(local ?? raw, 'shortDate', this.locale);
      case 'multiselect':
        return rowData[col.field];
      case 'boolean':
        return rowData[col.field] == true
          ? this.translocoService.translate('Yes')
          : this.translocoService.translate('No');
      case 'numeric':
        if (rowData[col.field] == null) return null;
        return formatNumber(
          rowData[col.field],
          this.locale,
          col.decimalPlaces != null
            ? `1.${col.decimalPlaces}-${col.decimalPlaces}`
            : '1.0-2',
        );
      case 'blob':
        return getHtmlImgDisplayString64(rowData[col.field]);
      default:
        return null;
    }
  }

  colTrackByFn(index, item) {
    return item.field;
  }

  actionTrackByFn(index, item: Action) {
    return `${index}${item.field}`;
  }

  exportListToExcel() {
    let filter: Filter = this.lastLazyLoadEvent as unknown as Filter;
    filter.additionalFilterIdLong = this.additionalFilterIdLong;

    exportListToExcel(this.exportListToExcelObservableMethod, filter);
  }

  clear(table: Table) {
    table.clear();
    table.clearState();
  }

  //#region Selection

  setFakeIsAllSelected() {
    if (this.rowsSelectedNumber == this.totalRecords)
      this.fakeIsAllSelected = true;
    else this.fakeIsAllSelected = false;
  }

  selectAll(checked: boolean) {
    this.unselectedItems.length = 0;
    this.newlySelectedItems.length = 0;

    if (checked == true) {
      this.isAllSelected = true;
      this.fakeIsAllSelected = true;
      this.onIsAllSelectedChange.next(
        new AllClickEvent({
          checked: true,
          additionalIndexes: this.additionalIndexes,
        }),
      );
      this.rowsSelectedNumber = this.totalRecords;
      this.fakeSelectedItems = [...this.items.map((x) => x[this.idField])];
      this.selectedItemIds = [...this.items.map((x) => x[this.idField])];
    } else {
      this.isAllSelected = false;
      this.fakeIsAllSelected = false;
      this.onIsAllSelectedChange.next(
        new AllClickEvent({
          checked: false,
          additionalIndexes: this.additionalIndexes,
        }),
      );
      this.rowsSelectedNumber = 0;
      this.fakeSelectedItems = [];
      this.selectedItemIds = [];
    }
  }

  selectRow(id: number, index: number) {
    if (this.isRowSelected(id)) {
      this.rowUnselect(id);
      this.onRowUnselect.next(
        new RowClickEvent({
          index: index,
          id: id,
          additionalIndexes: this.additionalIndexes,
        }),
      );
    } else {
      this.rowSelect(id);
      this.onRowSelect.next(
        new RowClickEvent({
          index: index,
          id: id,
          additionalIndexes: this.additionalIndexes,
        }),
      );
    }
  }

  isRowSelected(id: number) {
    if (this.hasLazyLoad) {
      return this.fakeSelectedItems.find((x) => x === id) != undefined;
    } else {
      return this.selectedItemIds.find((x) => x === id) != undefined;
    }
  }

  rowSelect(id: number) {
    if (
      this.isAllSelected == false ||
      this.currentPageSelectedItemsFromDb.includes(id) == false
    ) {
      this.newlySelectedItems.push(id);
    }

    if (this.hasLazyLoad) {
      this.fakeSelectedItems.push(id);
    } else {
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
    if (
      this.isAllSelected == true ||
      this.currentPageSelectedItemsFromDb.includes(id) == true
    ) {
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
  getFormArrayControlByIndex(
    formControlName: string,
    index: number,
  ): SpiderlyFormControl {
    if (this.getFormControl) {
      return this.getFormControl(
        formControlName,
        index,
        this.additionalIndexes,
      );
    } else {
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
  styleClass?: string;
  /**
   * Fired when a custom action is clicked. Receives an {@link ActionClickEvent} with the
   * row id, the full row object, the clicked DOM element (use it to anchor an overlay/popover),
   * and the original `MouseEvent`.
   *
   * Only fires for custom actions — the built-in `field` values `'Details'` and `'Delete'`
   * are handled internally and never invoke `onClick`.
   */
  onClick?: (event: ActionClickEvent) => void;

  constructor({
    name,
    field,
    icon,
    style,
    styleClass,
    onClick,
  }: {
    name?: string;
    field?: string;
    icon?: string;
    style?: string;
    styleClass?: string;
    onClick?: (event: ActionClickEvent) => void;
  } = {}) {
    this.name = name;
    this.field = field;
    this.icon = icon;
    this.style = style;
    this.styleClass = styleClass;
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
  decimalPlaces?: number;
  sortable?: boolean;
  /**
   * Whether the column is initially rendered. Defaults to `true`. Declare `visible: false` for
   * columns that should be available in the column chooser but hidden until the user reveals them.
   */
  visible?: boolean;
  /**
   * Pins the column: it always renders and shows in the chooser as checked and disabled.
   * Use for the row's identifying column (e.g. a product's title) so the table can never
   * lose its anchor.
   */
  lockVisible?: boolean;
  /**
   * Fired when this column's cell is clicked. Receives a {@link CellClickEvent} with the row id,
   * the column field, the full row, the raw and formatted cell value, the clicked `<td>` element
   * (use it to anchor an overlay/popover), and the original `MouseEvent`.
   *
   * Setting this makes the cell visibly clickable (cursor + hover) and the click no longer bubbles
   * up to row navigation. The mirror of {@link Action.onClick}, but for plain value cells.
   * Not applied to editable cells — those belong to their inline input.
   */
  onCellClick?: (event: CellClickEvent) => void;

  constructor({
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
    decimalPlaces,
    sortable,
    visible,
    lockVisible,
    onCellClick,
  }: {
    name?: string;
    field?: string & keyof T;
    filterField?: string & keyof T; // Made specificaly for multiautocomplete, maybe for something more in the future;
    filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
    filterPlaceholder?: string;
    showMatchModes?: boolean;
    showAddButton?: boolean;
    dropdownOrMultiselectValues?: PrimengOption[];
    actions?: Action[];
    editable?: boolean;
    showTime?: boolean;
    decimalPlaces?: number;
    sortable?: boolean;
    visible?: boolean;
    lockVisible?: boolean;
    onCellClick?: (event: CellClickEvent) => void;
  } = {}) {
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
    this.decimalPlaces = decimalPlaces;
    this.sortable = sortable;
    this.visible = visible;
    this.lockVisible = lockVisible;
    this.onCellClick = onCellClick;
  }
}

/**
 * Payload passed to {@link Action.onClick} when a custom row action is clicked.
 * Every field is populated by the data table, so consumers can rely on them being present.
 */
export interface ActionClickEvent {
  /** The clicked row's id (`row[idField]`). */
  id: number;
  /** The full row object the action belongs to. */
  row: any;
  /**
   * The clicked action element — pass it as the anchor when opening an overlay/popover.
   * Captured at click time on purpose: `originalEvent.currentTarget` is reset to null once
   * dispatch ends, so it would already be null inside an async handler.
   */
  element: HTMLElement;
  /** The original DOM click event. */
  originalEvent: MouseEvent;
}

/**
 * Payload passed to {@link Column.onCellClick} when a cell is clicked. A superset of
 * {@link ActionClickEvent} (same `id` / `row` / `element` / `originalEvent`) plus the cell's
 * column and value. Every field is populated by the data table.
 */
export interface CellClickEvent extends ActionClickEvent {
  /** The clicked column's `field`. */
  field: string;
  /** The cell's raw value (`row[field]`). */
  value: any;
  /** The formatted value shown in the cell (what the table renders via `getRowData`). */
  displayValue: string;
}

export class RowClickEvent {
  index?: number;
  id?: number;
  additionalIndexes?: any;

  constructor({
    index,
    id,
    additionalIndexes,
  }: {
    index?: number;
    id?: number;
    additionalIndexes?: any;
  } = {}) {
    this.index = index;
    this.id = id;
    this.additionalIndexes = additionalIndexes;
  }
}

export class AllClickEvent {
  checked?: boolean;
  additionalIndexes?: any;

  constructor({
    checked,
    additionalIndexes,
  }: {
    checked?: boolean;
    additionalIndexes?: any;
  } = {}) {
    this.checked = checked;
    this.additionalIndexes = additionalIndexes;
  }
}
