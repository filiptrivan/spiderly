import { CommonModule } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ContentChild,
  ContentChildren,
  ElementRef,
  EventEmitter,
  Input,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  QueryList,
  SimpleChanges,
  TemplateRef,
  viewChild,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective } from '@jsverse/transloco';
import { SortMeta } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { Popover, PopoverModule } from 'primeng/popover';
import { Table, TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { Observable } from 'rxjs';
import { SpiderlyControlsModule } from '../../controls/spiderly-controls.module';
import { SpiderlyCellTemplateDirective } from '../../directives/spiderly-cell-template.directive';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { SpiderlyOverflowTitleDirective } from '../../directives/spiderly-overflow-title.directive';
import { Filter } from '../../entities/filter';
import { FilterRule } from '../../entities/filter-rule';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { PaginatedResult } from '../../entities/paginated-result';
import { FilterSource, SortKeyLabel, SortPickOption } from '../../filters/filter-store';
import { SpiderlyFilterTemplateDirective } from '../../directives/spiderly-filter-template.directive';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { ConfigServiceBase } from '../../services/config.service.base';
import { exportListToExcel } from '../../services/helper-functions';
import { SpiderlyFormControl } from '../spiderly-form-control/spiderly-form-control';
import { provideSpiderlyScope } from '../../i18n/spiderly-scope';
import { ActiveView } from './active-view';
import { CellDisplay } from './cell-display';
import { ColumnLayout, isDataColumn } from './column-layout';
import { ColumnResize } from './column-resize';
import { FIXED_TABLE_LAYOUT } from './column-shares';
import { Column } from './data-table-column';
import { FrozenEdge } from './frozen-edge';
import { HeaderDrag } from './header-drag';
import { HeaderMenu } from './header-menu';
import { PageLoader } from './page-loader';
import { RowActions } from './row-actions';
import { AllClickEvent, RowClickEvent, RowSelection } from './row-selection';
import { isColumnSortable, SortResolution } from './sort-resolution';
import { TableSort } from './table-sort';
import { TableStateStorage } from './table-state-storage';
import { TableView } from './table-view';
import { ViewSwitch } from './view-switch';

// The names this file exported before its jobs moved to their own files; `public-api.ts` and
// consumers still import them from here.
export { Action, Column } from './data-table-column';
export type { ActionClickEvent, CellClickEvent } from './data-table-column';
export { AllClickEvent, RowClickEvent } from './row-selection';
export type { TableView } from './table-view';

/**
 * Wraps PrimeNG's `<p-table>` with Spiderly's column, filter, sort and paging model.
 *
 * The operator-owned view (the consumer's filter store and its chip bar, the header menu, widths,
 * header drag, the frozen left edge, views) has its decision record in
 * `docs/data-table-operator-owned-view.md`. A comment in this library that cites "decision N"
 * names the decision with that number there.
 *
 * Each job of the table is a module beside this file. This class declares the inputs, and it
 * composes the modules.
 */
@Component({
  selector: 'spiderly-data-table',
  templateUrl: './spiderly-data-table.component.html',
  providers: [provideSpiderlyScope()],
  styleUrl: 'spiderly-data-table.component.scss',
  imports: [
    FormsModule,
    CommonModule,
    TranslocoDirective,
    SpiderlyControlsModule,
    TableModule,
    ButtonModule,
    PopoverModule,
    CheckboxModule,
    SpiderlyFilterBarComponent,
    SpiderlyOverflowTitleDirective,
    TooltipModule,
  ],
})
export class SpiderlyDataTableComponent
  implements OnInit, OnChanges, AfterViewInit, OnDestroy
{
  @ViewChild('dt') table: Table;

  // The table's jobs, one module each. A module reads the inputs below through the `this` it is
  // handed, so it sees a later input change as the table does.
  private readonly storage = new TableStateStorage(this);
  protected readonly activeView = new ActiveView(this, this.storage);
  protected readonly layout = new ColumnLayout(
    this,
    this.storage,
    this.activeView,
    (hidden) => this.sort.dropHiddenColumnSort(hidden),
  );
  private readonly sortResolution = new SortResolution(
    this,
    this.storage,
    this.activeView,
    this.layout,
  );
  protected readonly sort = new TableSort(this, this.sortResolution);
  protected readonly frozenEdge = new FrozenEdge(this, this.layout);
  protected readonly headerDrag = new HeaderDrag(this.layout);
  protected readonly columnResize = new ColumnResize(this.layout);
  protected readonly selection = new RowSelection(this);
  protected readonly cells = new CellDisplay(this);
  protected readonly rowActions = new RowActions(this, this.selection, this.cells);
  protected readonly pageLoader = new PageLoader(
    this,
    this.storage,
    this.activeView,
    this.sort,
    this.selection,
  );
  private readonly viewSwitch = new ViewSwitch(
    this,
    this.activeView,
    this.layout,
    this.sort,
    this.pageLoader,
  );

  /**
   * A ViewChild rather than a template reference passed in from the header. The popover lives in
   * the CAPTION template and the button that opens it in the HEADER one, and a reference variable
   * does not cross an ng-template boundary — it arrived as undefined, and the click did nothing
   * at all.
   */
  private readonly columnMenu = viewChild.required<Popover>('columnMenu');

  private readonly filterBar = viewChild(SpiderlyFilterBarComponent);

  /**
   * Consumer-projected filter controls, forwarded to the bar. Collected here rather than by the
   * bar itself because the bar is OURS: a consumer's template is projected at the table, and
   * content queries only see what was projected into the component that declares them.
   */
  @ContentChildren(SpiderlyFilterTemplateDirective)
  filterTemplates: QueryList<SpiderlyFilterTemplateDirective>;

  private readonly selectionHeader =
    viewChild<ElementRef<HTMLElement>>('selectionHeader');

  protected readonly headerMenu = new HeaderMenu(
    this,
    this.columnMenu,
    this.filterBar,
    this.layout,
    this.sort,
    this.columnResize,
  );

  /**
   * Custom toolbar content projected via `<ng-template spiderlyDataTableActions>`.
   * Rendered in the caption action area ahead of the built-in buttons.
   */
  @ContentChild(SpiderlyDataTableActionsDirective, { read: TemplateRef })
  actionsTemplate: TemplateRef<any>;

  /**
   * Per-column cell templates projected via `<ng-template spiderlyCellTemplate="field">`.
   * A column with no template keeps the built-in rendering.
   */
  @ContentChildren(SpiderlyCellTemplateDirective)
  cellTemplates: QueryList<SpiderlyCellTemplateDirective>;

  @Input() tableTitle: string;
  @Input() tableIcon: string = 'pi pi-list';
  @Input() items: any[]; // Pass only when hasLazyLoad === false
  @Input() rows: number;
  /** Paginator page-size choices; merge rules: `mergeActivePageSizesIntoOptions()`. */
  @Input() rowsPerPageOptions: number[] = [10, 25, 50, 100];
  @Input() cols: Column[];

  /**
   * The consumer's filter store — the table's WHOLE filter surface, rendered as the chip bar. A
   * table given none has no filtering at all (`Column.filterType` is only the value's shape; the
   * legacy header-filter fallback was deleted — `docs/data-table-operator-owned-view.md`).
   *
   * READ ONCE, in ngOnInit — a store assigned later is silently ignored: the requery effect
   * stays subscribed to the first store's `applied()`, and `resolvedStateKey` (which
   * `additionalFilterIdLong` feeds) is never re-derived either. A consumer that must swap
   * stores at runtime destroys and recreates the table around the swap — a consuming
   * app's page that swaps them nulls its view model for exactly this. Teaching
   * ngOnChanges to re-arm both is the open upstream fix.
   */
  @Input() filters?: FilterSource;

  /** Saved questions for this table, rendered as tabs above the bar. */
  @Input() views?: TableView[];

  /** The view currently selected: `ActiveView.id`. */
  get activeViewId(): string | null {
    return this.activeView.id;
  }

  /** Whether the paginator is shown. Pass only when `hasLazyLoad === false`. Defaults to `true`. */
  @Input() showPaginator: boolean = true;
  /** Whether the table is wrapped in a card container. Defaults to `false`. */
  @Input() showCardWrapper: boolean = false;
  @Input() readonly: boolean = false;
  @Input() idField = 'id';

  /** Sizes the columns from their declared widths: `FIXED_TABLE_LAYOUT`. */
  protected readonly tableStyle = FIXED_TABLE_LAYOUT;
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

  // The selection's state lives in `RowSelection`; these are its inputs and the fields that
  // consumers read.
  @Input() set newlySelectedItems(ids: number[]) {
    this.selection.newlySelectedItems = ids;
  }
  get newlySelectedItems(): number[] {
    return this.selection.newlySelectedItems;
  }
  @Input() set unselectedItems(ids: number[]) {
    this.selection.unselectedItems = ids;
  }
  get unselectedItems(): number[] {
    return this.selection.unselectedItems;
  }
  get rowsSelectedNumber(): number {
    return this.selection.rowsSelectedNumber;
  }
  get selectedItemIds(): number[] {
    return this.selection.selectedItemIds;
  }
  @Input() selectionMode: 'single' | 'multiple' | undefined | null;
  @Output() onLazyLoad: EventEmitter<Filter> = new EventEmitter();
  @Output() onIsAllSelectedChange: EventEmitter<AllClickEvent> =
    new EventEmitter();
  @Input() selectedLazyLoadObservableMethod: (
    tableFilter: Filter,
  ) => Observable<LazyLoadSelectedIdsResult>;
  @Input() additionalFilterIdLong: number;

  /** Whether the "Add" button is shown. Defaults to `true`. */
  @Input() showAddButton: boolean = true;
  /** Whether the "Export to Excel" button is shown. Defaults to `true`. */
  @Input() showExportToExcelButton: boolean = true;
  /** Whether the reload-table button is shown. Defaults to `false`. */
  @Input() showReloadTableButton: boolean = false;

  // Client side table
  // @Input() formArrayItems: any[]; // Pass this only if you have some additional logic for showing data
  @Input() getFormArrayItems: (additionalIndexes?: any) => any[];
  /**
   * Whether the server pages, sorts and filters the rows. The column chooser and each header's
   * menu, resize grip and drag render only for a lazy table: a client-side (form-array) table
   * keeps its declared columns.
   */
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

  /** Columns currently rendered: `ColumnLayout.visibleCols`. */
  get visibleCols(): Column[] {
    return this.layout.visibleCols;
  }

  /** The key the table's state persists under: `TableStateStorage.resolvedStateKey`. */
  get resolvedStateKey(): string | null {
    return this.storage.resolvedStateKey;
  }
  @Input() getAlreadySelectedItemIds: (additionalIndexes?: any) => number[]; // Pass only when hasLazyLoad === false, it's enough if the M2M association hasn't additional fields
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

  constructor(private configService: ConfigServiceBase) {}

  ngAfterViewInit(): void {
    this.sort.setupRemovableSort();
    this.frozenEdge.observeSelectionWidth(this.selectionHeader()?.nativeElement);
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Only meaningful once ngOnInit has resolved the defaults; before that it is a no-op re-run.
    if (this.rows != null) this.pageLoader.mergeActivePageSizesIntoOptions();
    const viewsChange = changes['views'];
    if (viewsChange && !viewsChange.firstChange) {
      this.viewSwitch.adoptStoredView(viewsChange.previousValue);
      // The active view's `columns` may differ in the new array, or the view may be gone from it.
      this.layout.refresh();
    }
  }

  ngOnInit(): void {
    // Fail loud, not half-broken: the store commits through the paginated request, and a
    // client-side (form-array) table has none — armed anyway, the first commit would call the
    // absent getPaginatedListObservableMethod inside the requery effect and strand the pending
    // overlay forever. Client-side store predicates are unbuilt (spiderly#407 territory).
    if (this.filters && !this.hasLazyLoad) {
      throw new Error(
        'spiderly-data-table: [filters] requires a lazy table — client-side filtering through a store is not implemented. Remove [filters] or make the table lazy.',
      );
    }

    if (this.rows == null) this.rows = this.configService.defaultPageSize;

    this.activeView.id ??= this.views?.[0]?.id ?? null;

    if (this.deleteListFromTableObservableMethod && !this.selectionMode) {
      this.selectionMode = 'multiple';
    }

    if (this.hasLazyLoad) this.storage.resolveKey();
    else this.clientLoad();

    // After `resolvedStateKey` is derived, like the restores below: the merge reads the persisted
    // pick by that key. PrimeNG's own table state persists the pick (`saveState` and
    // `restoreState` carry `rows`).
    this.pageLoader.mergeActivePageSizesIntoOptions();

    // AFTER `resolvedStateKey` is derived, which is what every restore below keys off — placed
    // above it they read a null key and silently restore nothing. The view restore runs FIRST,
    // because the layout, the filters and the sort read the view it decides. And all of it before
    // the effect is armed, so the first request already carries the filters and the effect's
    // skipped first run sees the state it will be watching.
    const restoredView = this.activeView.restoreActiveView();
    this.layout.init();

    this.activeView.enterViewFilters(restoredView, 'refresh');
    if (this.filters) this.pageLoader.requeryOnAppliedFilters();

    this.sort.setPickOptions(this.layout.chooserCols);

    const tableState = this.storage.readTableState();
    this.initialMultiSortMeta =
      this.sortResolution.initialMultiSortMeta(tableState);
    this.sortResolution.alignPersistedTableStateSort(
      tableState,
      this.initialMultiSortMeta ?? [],
    );
  }

  /** Switches to a view: `ViewSwitch.selectView`. */
  selectView(view: TableView): void {
    this.viewSwitch.selectView(view);
  }

  /** The `filters` payload a view's own list request carries: `ActiveView.viewFilterPayload`. */
  viewFilterPayload(viewId: string): Record<string, FilterRule[]> | null {
    return this.activeView.viewFilterPayload(viewId);
  }

  // Questions a consumer or a spec asks the table; each module's doc tells its answer.
  readonly isColumnSortable = isColumnSortable;
  readonly isDataColumn = isDataColumn;

  columnShare(col: Column): number {
    return this.layout.columnShare(col);
  }

  getColWidth(col: Column): string {
    return this.layout.getColWidth(col);
  }

  isColumnWrapped(col: Column): boolean {
    return this.layout.isColumnWrapped(col);
  }

  canMoveColumn(col: Column, direction: -1 | 1): boolean {
    return this.layout.canMoveColumn(col, direction);
  }

  getRowData(rowData: any, col: Column): string {
    return this.cells.getRowData(rowData, col);
  }

  get sortKeys(): SortKeyLabel[] {
    return this.sort.sortKeys;
  }

  get sortPickOptions(): SortPickOption[] {
    return this.sort.sortPickOptions;
  }

  selectAll(checked: boolean): void {
    this.selection.selectAll(checked);
  }

  /** Whether a fetch is in flight: `PageLoader.isPending`. */
  get isPending(): boolean {
    return this.pageLoader.isPending;
  }

  lazyLoad(event: TableLazyLoadEvent) {
    this.pageLoader.load(event);
  }

  clientLoad() {
    this.pageLoader.loadClient();
  }

  reload() {
    this.pageLoader.reload();
  }

  ngOnDestroy(): void {
    this.frozenEdge.disconnect();
    this.columnResize.abort();
  }

  showActions(): boolean {
    return this.cols.some((x) => x.actions?.length > 0);
  }

  exportListToExcel() {
    let filter: Filter = this.lastLazyLoadEvent as unknown as Filter;
    filter.additionalFilterIdLong = this.additionalFilterIdLong;

    exportListToExcel(this.exportListToExcelObservableMethod, filter);
  }

  /**
   * Clears the FILTERS — and nothing else (2026-09-06, grilled). It used to also reach the
   * sort and PrimeNG's persisted state, which made "Clear filters" a reset the label never
   * claimed: sort is its own channel with its own affordances (the bar's picker, the header's
   * tri-state), mirroring Polaris, where onClearAll is wired to the Filters while
   * sortSelected never passes through it. The store's requery effect owns the refetch and the
   * persisted-snapshot update — the button only renders with chips on the bar, so the applied
   * set always changes — and dropping table.clear() also drops the second, unsequenced
   * request the 2026-09-05 review flagged on every Clear.
   */
  clear() {
    this.filters?.clear();
  }
}
