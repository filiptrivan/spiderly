import { CommonModule, formatDate, formatNumber } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ContentChild,
  ContentChildren,
  ElementRef,
  ErrorHandler,
  EventEmitter,
  effect,
  inject,
  Inject,
  Injector,
  Input,
  LOCALE_ID,
  NgZone,
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
import { ActivatedRoute, Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { SortMeta } from 'primeng/api';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogService, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Popover, PopoverModule } from 'primeng/popover';
import { Table, TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { firstValueFrom, Observable, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { SpiderlyControlsModule } from '../../controls/spiderly-controls.module';
import {
  CellTemplateContext,
  SpiderlyCellTemplateDirective,
} from '../../directives/spiderly-cell-template.directive';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { SpiderlyOverflowTitleDirective } from '../../directives/spiderly-overflow-title.directive';
import { Filter } from '../../entities/filter';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { PaginatedResult } from '../../entities/paginated-result';
import { PrimengOption } from '../../entities/primeng-option';
import { FilterSource, SortKeyLabel } from '../../filters/filter-store';
import { SpiderlyFilterTemplateDirective } from '../../directives/spiderly-filter-template.directive';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { ConfigServiceBase } from '../../services/config.service.base';
import {
  exportListToExcel,
  getHtmlImgDisplayString64,
  parseDateOnlyLocal,
  scrollElementIntoViewIfAboveViewport,
} from '../../services/helper-functions';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { readStoredJson, writeStoredJson } from '../../services/web-storage';
import {
  DeleteConfirmationData,
  SpiderlyDeleteConfirmationComponent,
} from '../spiderly-delete-dialog/spiderly-delete-confirmation.component';
import { SpiderlyFormControl } from '../spiderly-form-control/spiderly-form-control';

/**
 * Default column width per value type, in rem — generous on purpose (these numbers date from when
 * the header carried a filter input, and nobody has re-derived them from values yet), and
 * {@link Column.width} is the per-column override.
 *
 * A table, not a switch, so the mapping is exhaustive: a new `filterType` fails the build here
 * instead of silently inheriting the actions-column reservation below.
 */
/** One rem in pixels. Both the share arithmetic and the px-to-share conversion need it. */
function rootFontSizePx(): number {
  return parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
}

/** Order matters: `[a, b]` and `[b, a]` ask the server for different orderings. */
function sortMetaEquals(a: SortMeta[], b: SortMeta[]): boolean {
  return (
    a.length === b.length &&
    a.every((m, i) => m.field === b[i].field && m.order === b[i].order)
  );
}

const DEFAULT_COLUMN_WIDTH_REM: Record<
  NonNullable<Column['filterType']>,
  number
> = {
  text: 12,
  date: 10,
  multiselect: 12,
  boolean: 8,
  numeric: 12,
  // Fits the 45px thumbnail `#defaultCell` renders for a blob.
  blob: 5,
};

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
  private readonly destroy$ = new Subject<void>();

  @ViewChild('dt') table: Table;

  private readonly injector = inject(Injector);
  private readonly zone = inject(NgZone);

  /**
   * The selection column's declared share. Under the fixed layout this is a RATIO, not a width.
   *
   * 4rem, down from 6: the original reserved room for a five-digit select-all counter sitting at
   * body text size beside the box, which is the widest thing it can ever hold and not what it
   * holds on any ordinary screen. The counter is a smaller step now (see the SCSS), so the same
   * five digits fit in two thirds of the space.
   */
  protected readonly selectionColumnWidth = '4rem';

  /**
   * The selection column's MEASURED width, which is what the frozen identity column offsets
   * itself by. Declaring `6rem` on both looked like the one-telling answer and is wrong: widths
   * are shares, so that column renders at whatever proportion of the table it gets — 148px for a
   * declared 6rem in one spec — and the identity column would sit 52px inside it, overlapping the
   * checkboxes the moment anyone scrolled. The spec caught it; nothing about the page would have.
   */
  protected frozenOffsetPx = 0;

  private selectionWidthObserver?: ResizeObserver;

  /** Aborts an in-flight column resize's document listeners, from `onUp` or from teardown. */
  private resizeAbort: AbortController | null = null;

  /**
   * Whether this column is pinned against the left edge. Only the FIRST column, and only when it
   * is `lockVisible`: a sticky column in the middle of the grid pins the wrong thing and leaves a
   * hole where it used to be. Once the grid scrolls sideways a row otherwise loses its identity
   * — "Intesa, plaćeno, 12.400" with no idea whose order it is — and NN/g's Data Tables is
   * explicit that the leftmost header column must lock in place. `lockVisible` already names
   * exactly that column in every table, so this needs no new API (CLAUDE.md -> decision 8).
   */
  isColumnFrozen(col: Column): boolean {
    return col.lockVisible === true && this.visibleCols[0] === col;
  }

  /**
   * The rightmost pinned cell, which carries the seam's shadow. It is the identity column when
   * there is one and the checkbox column otherwise — a fact only this side knows, which is why
   * the SCSS asking for it as `:last-of-type` drew nothing at all.
   */
  isLastFrozenColumn(col: Column): boolean {
    return this.isColumnFrozen(col);
  }

  get isSelectionColumnLastFrozen(): boolean {
    return !this.visibleCols.some((col) => this.isColumnFrozen(col));
  }

  /** A frozen column starts after the checkbox column, when there is one. */
  frozenColumnOffset(): string {
    return this.selectionMode === 'multiple' ? `${this.frozenOffsetPx}px` : '0';
  }

  /**
   * Tracks the selection column's rendered width. A ResizeObserver rather than a one-off read:
   * every column drag and every window resize re-splits the shares, so the offset moves without
   * anything about the selection column itself changing.
   */
  private observeSelectionWidth(): void {
    const cell = this.selectionHeader()?.nativeElement;
    if (!cell || this.selectionWidthObserver) return;

    const measure = () => {
      const width = cell.offsetWidth;
      if (width === this.frozenOffsetPx) return;

      this.zone.run(() => (this.frozenOffsetPx = width));
    };

    if (typeof ResizeObserver !== 'undefined') {
      this.zone.runOutsideAngular(() => {
        this.selectionWidthObserver = new ResizeObserver(measure);
        this.selectionWidthObserver.observe(cell);
      });
    }

    // Measured once immediately as well, on a microtask. The observer's first callback arrives an
    // animation frame later, and until it does the identity column sits at left:0 — on top of the
    // checkbox column it is supposed to start after. The microtask keeps the write out of the CD
    // pass that just finished, which doing it inline here would break with an NG0100.
    queueMicrotask(measure);
  }

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
  /** Paginator page-size choices; merge rules and persistence: CLAUDE.md → "Rows-per-page". */
  @Input() rowsPerPageOptions: number[] = [10, 25, 50, 100];
  @Input() cols: Column[];

  /**
   * The consumer's filter store — the table's WHOLE filter surface, rendered as the chip bar. A
   * table given none has no filtering at all (`Column.filterType` is only the value's shape; the
   * legacy header-filter fallback was deleted — CLAUDE.md -> "Operator-owned view").
   *
   * READ ONCE, in ngOnInit — a store assigned later is silently ignored: the requery effect
   * stays subscribed to the first store's `applied()`, and `resolvedStateKey` (which
   * `additionalFilterIdLong` feeds) is never re-derived either. A consumer that must swap
   * stores at runtime destroys and recreates the table around the swap — PACMS
   * `integration-matching-products` nulls its view model for exactly this. Teaching
   * ngOnChanges to re-arm both is the open upstream fix.
   */
  @Input() filters?: FilterSource;

  /** Saved questions for this table, rendered as tabs above the bar. */
  @Input() views?: TableView[];

  /**
   * The view currently selected. Seeded from the first view in `ngOnInit` — then overridden by
   * `restoreActiveView`, so a reload lands where the operator was — and never left null on a
   * table that has views: `viewScope` keys the layout off it, so an unseeded table writes its
   * width and order to the SAME key an unmigrated table uses — orphaned the moment the operator
   * clicks the first tab — and no tab reads as selected until they do.
   */
  activeViewId: string | null = null;
  /** Whether the paginator is shown. Pass only when `hasLazyLoad === false`. Defaults to `true`. */
  @Input() showPaginator: boolean = true;
  /** Whether the table is wrapped in a card container. Defaults to `false`. */
  @Input() showCardWrapper: boolean = false;
  @Input() readonly: boolean = false;
  @Input() idField = 'id';

  /**
   * Sizes the columns from their declared widths instead of from the rendered rows.
   *
   * The browser default is `table-layout: auto`, which computes every column from the content
   * of the rows currently on screen — so a column is one width on this page and another on the
   * next, and a filter (which narrows the variety of values in the column it filters) moves the
   * whole grid. PrimeNG sets no `table-layout` of its own, so without this the table inherits
   * that default. Bound to a field, never an object literal in the template: a literal is a new
   * reference on every change-detection pass.
   */
  protected readonly tableStyle = { 'table-layout': 'fixed' };
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
  private loading: boolean = true;

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
  /** Shift-range selection state — model and invariants: CLAUDE.md → "Shift-click range selection". */
  private rangeAnchorId: number | null = null;
  private pendingShift: { shiftKey: boolean; id: number } | null = null;
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
    private errorHandler: ErrorHandler,
    @Inject(LOCALE_ID) private locale: string,
  ) {}

  ngAfterViewInit(): void {
    this.setupRemovableSort();
    this.observeSelectionWidth();
  }

  /**
   * The template that renders this column's cells, or null to use the built-in rendering.
   * Actions columns have no `field`, so they never match one.
   *
   * Read straight off the QueryList, which is live — a consumer declaring its templates inside a
   * `@for`/`@if` is served with no subscription and nothing to invalidate. The scan is over the
   * TEMPLATED columns only (typically one or two), not over `cols`.
   */
  getCellTemplate(col: Column): TemplateRef<CellTemplateContext> | null {
    if (col.field == null) return null;
    return (
      this.cellTemplates?.find((directive) => directive.field === col.field)
        ?.template ?? null
    );
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
      } else {
        originalSort(event);
      }

      // Every explicit header-sort gesture, un-sort included, records the view's override.
      this.persistActiveViewSort();
    };
  }

  /**
   * A page size PrimeNG will actually apply but that is missing from `rowsPerPageOptions` leaves
   * the paginator dropdown blank — so both the effective `rows` and the persisted pick (which
   * `restoreState` applies after init) are merged in. Runs again on input changes because a
   * consumer may resolve `[rows]` asynchronously. Storage is user-writable, hence the shape and
   * ceiling check: a hand-edited `{"rows": 100000}` must not become an offered choice, since the
   * backend `.Take`s whatever it is sent.
   */
  private mergeActivePageSizesIntoOptions(): void {
    const ceiling = Math.max(...this.rowsPerPageOptions);
    const persisted = this.persistedTableState()?.rows;
    const candidates = [
      this.rows,
      Number.isInteger(persisted) && persisted > 0 && persisted <= ceiling
        ? persisted
        : null,
    ];

    for (const value of candidates) {
      if (value != null && !this.rowsPerPageOptions.includes(value)) {
        this.rowsPerPageOptions = [...this.rowsPerPageOptions, value].sort(
          (a, b) => a - b,
        );
      }
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Only meaningful once ngOnInit has resolved the defaults; before that it is a no-op re-run.
    if (this.rows != null) this.mergeActivePageSizesIntoOptions();
    if (changes['views']) this.adoptStoredView();
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

    this.activeViewId ??= this.views?.[0]?.id ?? null;


    if (this.deleteListFromTableObservableMethod && !this.selectionMode) {
      this.selectionMode = 'multiple';
    }

    if (this.hasLazyLoad) {
      const baseKey = this.stateKey ?? `spiderly-table:${this.router.url}`;
      this.resolvedStateKey =
        this.additionalFilterIdLong != null
          ? `${baseKey}:${this.additionalFilterIdLong}`
          : baseKey;
    } else {
      this.clientLoad();
    }

    this.mergeActivePageSizesIntoOptions();

    // AFTER `resolvedStateKey` is derived, which is what every restore below keys off — placed
    // above it they read a null key and silently restore nothing. The view restore runs FIRST,
    // because the other three read the `viewScope` it decides. And all of it before the effect
    // is armed, so the first request already carries the filters and the effect's skipped first
    // run sees the state it will be watching.
    const restoredView = this.restoreActiveView();

    this.restoreColumnVisibility();
    this.restoreColumnLayout();

    this.enterViewFilters(restoredView, 'refresh');
    if (this.filters) this.requeryOnAppliedFilters();

    this.chooserCols = this.cols.filter(SpiderlyDataTableComponent.isDataColumn);
    this.refreshVisibleCols();

    // Bound to p-table's [multiSortMeta] so the FIRST request already carries the right
    // sort. The view's own sort wins; the persisted table-global sort sits deliberately
    // BETWEEN it and the table default, so a view declaring nothing keeps the ordering the
    // operator last chose. We read it all ourselves because PrimeNG's restoreState() runs on
    // the first [value] change — after the initial lazy emit has already left — and is then
    // told to agree (see `alignPersistedTableStateSort`).
    const tableState = this.persistedTableState();
    this.initialMultiSortMeta =
      this.viewSort() ??
      this.sanitizedSortMeta(tableState?.multiSortMeta) ??
      this.tableDefaultMultiSortMeta();
    this.alignPersistedTableStateSort(tableState);
  }

  //#region Column visibility

  /**
   * User visibility choices (field → visible), holding only fields the user explicitly
   * toggled away from their declared default — so a page changing a column's declared
   * default later flows through to every user who never touched that column.
   */
  private columnVisibilityOverrides: Record<string, boolean> = {};

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
    return this.resolvedStateKey
      ? `${this.resolvedStateKey}${this.viewScope}:columns`
      : null;
  }

  /** Actions columns always render; data columns follow the override, then declared default. */
  isColumnVisible(col: Column): boolean {
    if (!SpiderlyDataTableComponent.isDataColumn(col)) return true;
    if (col.lockVisible) return true; // pinned — wins over any (possibly stale) override
    return this.columnVisibilityOverrides[col.field] ?? col.visible !== false;
  }

  /**
   * `cols` in the operator's order. A field the stored order has never seen — a column added in a
   * later release — sorts to the END rather than displacing a layout someone built by hand
   * (CLAUDE.md -> "Operator-owned view", decision 7); the sort is stable, so those keep their
   * declared order among themselves. Actions columns carry no field and are always last, which is
   * where every consumer already declares them.
   */
  private orderedCols(): Column[] {
    if (this.columnOrder.length === 0) return this.cols;

    const rank = new Map(
      this.columnOrder.map((field, index) => [field, index]),
    );

    return [...this.cols].sort(
      (a, b) =>
        (rank.get(a.field!) ?? Number.MAX_SAFE_INTEGER) -
        (rank.get(b.field!) ?? Number.MAX_SAFE_INTEGER),
    );
  }

  /**
   * Whether this column can move one place in `direction` (-1 left, 1 right). A locked column is
   * the row's anchor — and, once the left edge freezes, the thing a horizontal scroll keeps in
   * view — so it does not move and nothing moves in front of it.
   */
  canMoveColumn(col: Column, direction: -1 | 1): boolean {
    if (col.lockVisible || !col.field) return false;

    const order = this.dataColumnsInOrder();
    const from = order.indexOf(col);
    const to = from + direction;

    if (from < 0 || to < 0 || to >= order.length) return false;

    return !order[to].lockVisible;
  }

  moveMenuColumn(direction: -1 | 1): void {
    const col = this.menuColumn;
    this.columnMenu().hide();
    if (!col || !this.canMoveColumn(col, direction)) return;

    const order = this.dataColumnsInOrder();
    this.placeColumn(order, col, order.indexOf(col) + direction);
  }

  /** The move itself, shared by the menu and the drag — two entry points, one rule. */
  private placeColumn(order: Column[], col: Column, to: number): void {
    order.splice(to, 0, ...order.splice(order.indexOf(col), 1));

    this.columnOrder = order.map((entry) => entry.field!);
    this.persistColumnLayout();
    this.refreshVisibleCols();
  }

  private dataColumnsInOrder(): Column[] {
    return this.orderedCols().filter(SpiderlyDataTableComponent.isDataColumn);
  }

  /**
   * Header drag, ours rather than PrimeNG's `pReorderableColumn`, for two reasons: its
   * `onMouseDown` arms the th for anything that is not an INPUT, a TEXTAREA or its OWN resizer —
   * so our menu chevron and our resize grip would each start a column drag, and reaching for a
   * width would silently reorder the grid — and its `onColumnDrop` reorders blind, with no notion
   * of a locked column (CLAUDE.md -> decision 6).
   *
   * A shortcut for neighbours, never the mechanism: HTML5 dnd has no edge auto-scroll, so a column
   * scrolled off the right is unreachable this way, and there is no keyboard path at all. The menu
   * carries both.
   *
   * This half is the arming rule; the drop rule is `canDropOn` below.
   */
  onHeaderMouseDown(event: MouseEvent): void {
    const target = event.target as HTMLElement;

    this.headerDragArmed = !target.closest(
      '.column-menu-button, .column-resizer',
    );
  }

  onHeaderDragStart(col: Column, event: DragEvent): void {
    if (col.lockVisible || !this.headerDragArmed) {
      event.preventDefault();
      return;
    }

    this.draggedColumn = col;
    event.dataTransfer?.setData('text/plain', col.field ?? '');
  }

  onHeaderDragOver(col: Column, event: DragEvent): void {
    if (this.canDropOn(col)) event.preventDefault();
  }

  onHeaderDrop(col: Column, event: DragEvent): void {
    event.preventDefault();

    const dragged = this.draggedColumn;
    this.draggedColumn = null;
    if (!dragged || dragged === col || !this.canDropOn(col)) return;

    const order = this.dataColumnsInOrder();
    this.placeColumn(order, dragged, order.indexOf(col));
  }

  /** Nothing lands on, or in front of, the locked column. Enforced here as well as in the menu:
   * a drop is a separate entry point, and the rule is about the grid rather than about a button. */
  private canDropOn(col: Column): boolean {
    return !col.lockVisible && SpiderlyDataTableComponent.isDataColumn(col);
  }

  private refreshVisibleCols(): void {
    this.visibleCols = this.orderedCols().filter((col) =>
      this.isColumnVisible(col),
    );
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
    if (visible === (col.visible !== false)) {
      delete this.columnVisibilityOverrides[col.field]; // back at the declared default
    } else {
      this.columnVisibilityOverrides[col.field] = visible;
    }

    this.persistColumnVisibility();
    this.refreshVisibleCols();

    if (!visible) this.dropHiddenColumnSort([col]);
  }

  /**
   * Restores every column to its declared default and forgets the stored overrides — visibility,
   * wrap, order and width alike. It undid visibility only until the header menu shipped three
   * more gestures beside it, and a layout with no way back is worse than one with no knobs.
   *
   * On a STORE table, hiding — here or from the menu — never touches filters or sort: the chip
   * bar names every applied filter and the sort chip names the sort, hidden column or not
   * (decision 2b). A storeless table keeps the sort half of the legacy invariant instead — see
   * `dropHiddenColumnSort`.
   */
  resetColumnLayout(): void {
    this.columnWrap = {};
    this.columnOrder = [];
    this.columnWidths = {};
    this.persistColumnLayout();

    const wasVisible = this.visibleCols.filter(
      SpiderlyDataTableComponent.isDataColumn,
    );

    this.columnVisibilityOverrides = {};
    this.persistColumnVisibility();
    this.refreshVisibleCols();

    // Columns the reset just hid follow the same rule as a manual hide.
    this.dropHiddenColumnSort(
      wasVisible.filter((col) => !this.isColumnVisible(col)),
    );
  }

  /**
   * The sort half of the legacy "hidden contributes nothing" invariant, kept ONLY for storeless
   * tables (the generated M2M details grids, until spiderly#407 scaffolds their stores): they
   * have no sort chip, so a hidden column's sort would order the grid with nothing on screen
   * naming it and no control left to clear it. The filter half needs no heir — `lazyLoad`
   * overwrites the outgoing filters unconditionally. On a store table this is a no-op:
   * decision 2b's chips are the visible surface, hidden column or not.
   */
  private dropHiddenColumnSort(cols: Column[]): void {
    if (!this.table || this.filters) return;

    const sorted = cols.filter((col) =>
      this.table._multiSortMeta?.some((m) => m.field === col.field),
    );
    if (!sorted.length) return;

    this.table._multiSortMeta = this.table._multiSortMeta.filter(
      (m) => !sorted.some((col) => col.field === m.field),
    );
    this.table.tableService.onSort(this.table._multiSortMeta);
    this.table._filter(); // re-emits the lazy load and saves the cleaned state
  }

  /**
   * Per-column wrap, by `field`. A SEPARATE key from the visibility overrides on purpose: folding
   * both into one object would invalidate every layout already in an operator's localStorage, and
   * this is the key the width and order overrides will join (decision 5).
   */
  private columnWrap: Record<string, boolean> = {};

  /** Data-column fields in the operator's order. Empty means "as declared". */
  private columnOrder: string[] = [];

  /**
   * Per-column width overrides, as SHARES in the same unit `getColWidth` emits — never pixels.
   * Under the fixed layout the browser splits surplus in proportion to these, so a pixel would
   * pin the column and stop it answering the window: the operator on a 1280 laptop and the one
   * on a 2560 monitor would get the same frozen column (decision 4).
   */
  private columnWidths: Record<string, number> = {};

  /**
   * Layout keys carry the ACTIVE VIEW, which is what makes a view more than a saved filter: a
   * picking view and a payments view want different COLUMNS, not just different rows. Global
   * layout would re-create the original complaint one level up — the operator still re-picking
   * columns every time the job changes.
   *
   * A table with no views has no segment, so the twenty-six tables that have not migrated keep
   * reading exactly the keys they already wrote.
   */
  private get viewScope(): string {
    return this.activeViewId ? `:${this.activeViewId}` : '';
  }

  /**
   * Where the applied filters live. Follows `stateStorage` rather than the layout's always-local
   * rule, because that is where they lived before the bar took them off the header: PrimeNG's
   * stateful table persisted them for free, and nothing replaced it — a refresh dropped every
   * filter (Filip, on /tags). A filter is a question you are in the middle of asking, not a
   * durable preference like a column's width.
   */
  private get filtersStateKey(): string | null {
    return this.resolvedStateKey
      ? `${this.resolvedStateKey}${this.viewScope}:filters`
      : null;
  }

  private get filterStorage(): Storage {
    return this.stateStorage === 'local' ? localStorage : sessionStorage;
  }

  /**
   * Where the active view id lives. Deliberately NOT view-scoped — it is the input `viewScope`
   * derives from — and it follows `stateStorage` like the filters rather than the layout's
   * always-local rule, because the id and the filters it scopes are one question: a durable id
   * over session-scoped filters would restore a tab with nothing under it.
   */
  private get viewStateKey(): string | null {
    return this.resolvedStateKey ? `${this.resolvedStateKey}:view` : null;
  }

  /** Only `selectView` writes; the `ngOnInit` seed does not — absence means "the default". */
  private persistActiveView(): void {
    if (!this.viewStateKey || !this.activeViewId) return;

    writeStoredJson(this.filterStorage, this.viewStateKey, this.activeViewId);
  }

  /**
   * Puts a reload back on the view the operator was using. Every persisted key carries the view
   * segment, so before this existed the seed decided what got restored: F5 landed on the first
   * view and read `…:all:filters` — the tab snapped to "Sve" and the bar came back empty while
   * the operator's filters sat intact under the old view's segment (Filip, on /porudzbine).
   *
   * An id naming no current view keeps the seed AND the stored key: "vanished by deploy" and
   * "not yet arrived" are indistinguishable here (order-list's catalog-licensed "Za pakovanje"
   * is literally both), so `adoptStoredView` re-reads the key on every `[views]` change and the
   * key self-heals on the next explicit click.
   */
  private restoreActiveView(): TableView | null {
    if (!this.viewStateKey || !this.views?.length) return null;

    const storedId = readStoredJson(this.filterStorage, this.viewStateKey);
    if (typeof storedId !== 'string') return null;

    const view = this.viewById(storedId) ?? null;
    if (view) this.activeViewId = view.id;

    return view;
  }

  /**
   * The second half of `restoreActiveView`: a stored view that did not exist at init is adopted
   * when a later `[views]` change brings it — through `selectView`, so it gets the same
   * stored-wins/transient/layout/requery semantics a click would have had. There is no parked
   * copy of the stored id — the key itself is re-read, and because every explicit pick
   * overwrites it, the operator's own choice wins by construction. Accepted cost of adopting
   * late instead of holding the first request: a brief flash of the seeded view's rows and a
   * second request when the late view lands.
   */
  private adoptStoredView(): void {
    if (!this.viewStateKey) return;

    const storedId = readStoredJson(this.filterStorage, this.viewStateKey);
    if (typeof storedId !== 'string' || storedId === this.activeViewId) return;

    const view = this.viewById(storedId);
    if (view) this.selectView(view);
  }

  private viewById(id: string): TableView | undefined {
    return this.views?.find((candidate) => candidate.id === id);
  }

  private persistAppliedFilters(): void {
    if (!this.filtersStateKey || !this.filters) return;

    const snapshot = this.filters.snapshot();

    if (Object.keys(snapshot).length === 0) {
      this.filterStorage.removeItem(this.filtersStateKey);
    } else {
      writeStoredJson(this.filterStorage, this.filtersStateKey, snapshot);
    }
  }

  /**
   * Applies the right filters for entering `view` — the transient rule's one home. A TRANSIENT
   * view's `apply` is a function of NOW, so it always re-derives; restoring would put
   * yesterday's date under a tab claiming "today" (`persistAppliedFilters` has no transient
   * guard, so storage holds the LAST answer). A non-transient view's own stored answer wins
   * over its `apply`. The semantics argument is the one deliberate difference between the
   * callers: a 'click' re-asks the view's question when nothing is stored, a 'refresh' returns
   * the empty state the operator left — they cleared it, and F5 must give the cleared state
   * back. On a table with no views, `view` is null and this is a plain restore of the
   * `persistAppliedFilters` snapshot.
   */
  private enterViewFilters(
    view: TableView | null,
    semantics: 'click' | 'refresh',
  ): void {
    if (!this.filters) return;

    const stored =
      !view?.transient && this.filtersStateKey
        ? readStoredJson(this.filterStorage, this.filtersStateKey)
        : null;

    if (stored) this.filters.restore(stored);
    else if (view?.transient || semantics === 'click') view?.apply?.(this.filters);
  }

  private get layoutStateKey(): string | null {
    return this.resolvedStateKey
      ? `${this.resolvedStateKey}${this.viewScope}:layout`
      : null;
  }

  /** One key for the whole layout — wrap now, widths next (decision 5). */
  private persistColumnLayout(): void {
    if (!this.layoutStateKey) return;

    const layout = {
      wrap: this.columnWrap,
      order: this.columnOrder,
      widths: this.columnWidths,
    };
    const isDefault =
      Object.keys(this.columnWrap).length === 0 &&
      this.columnOrder.length === 0 &&
      Object.keys(this.columnWidths).length === 0;

    if (isDefault) localStorage.removeItem(this.layoutStateKey);
    else writeStoredJson(localStorage, this.layoutStateKey, layout);
  }

  private restoreColumnLayout(): void {
    if (!this.layoutStateKey) return;

    const layout = readStoredJson(localStorage, this.layoutStateKey);
    this.columnWrap = layout?.wrap ?? {};
    this.columnOrder = layout?.order ?? [];
    this.columnWidths = layout?.widths ?? {};
  }

  private persistColumnVisibility(): void {
    if (!this.columnsStateKey) return;

    if (Object.keys(this.columnVisibilityOverrides).length === 0) {
      localStorage.removeItem(this.columnsStateKey);
    } else {
      writeStoredJson(
        localStorage,
        this.columnsStateKey,
        this.columnVisibilityOverrides,
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

  private activeView(): TableView | undefined {
    return this.activeViewId ? this.viewById(this.activeViewId) : undefined;
  }

  /**
   * Where the operator's per-view sort override lives. Null on a table without views — those
   * keep the pure PrimeNG sort persistence they always had — and null on a STORELESS table:
   * there "hidden contributes nothing" retires a hidden column's sort (`dropHiddenColumnSort`)
   * with no persist call at the hide site, so an override restored blind at the next init
   * would re-impose a sort no chip can name. Store tables — where every viewed consumer
   * lives — have the chip. Follows `stateStorage` like the filters and the view id: the sort
   * is part of the same question they scope.
   */
  private get viewSortStateKey(): string | null {
    if (!this.views?.length || !this.filters) return null;

    return this.resolvedStateKey
      ? `${this.resolvedStateKey}${this.viewScope}:sort`
      : null;
  }

  /** The operator's stored sort for the active view, sanitized like any persisted sort. */
  private persistedViewSort(): SortMeta[] | null {
    if (!this.viewSortStateKey) return null;

    const stored = readStoredJson(this.filterStorage, this.viewSortStateKey);
    return Array.isArray(stored) ? this.sanitizedSortMeta(stored) : null;
  }

  /** The sort this view asks for: the operator's stored override, else its declared sort. */
  private viewSort(): SortMeta[] | null {
    return this.persistedViewSort() ?? this.viewDeclaredMultiSortMeta();
  }

  /**
   * Records the current sort as the active view's override — called from the EXPLICIT sort
   * gestures only (header click, menu sort, Clear filters), never from a load: a sort merely
   * inherited at init must not crystallize into an override, or a later change to the view's
   * declared sort would stop flowing to operators who never touched sort — the same
   * flows-through rule `columnVisibilityOverrides` documents. A sort equal to the declared
   * default is stored as ABSENCE for the same reason.
   */
  private persistActiveViewSort(): void {
    const key = this.viewSortStateKey;
    if (!key) return;

    const current = this.table._multiSortMeta ?? [];

    if (sortMetaEquals(current, this.defaultMultiSortMeta() ?? [])) {
      this.filterStorage.removeItem(key);
    } else {
      writeStoredJson(this.filterStorage, key, current);
    }
  }

  /**
   * Puts the entering view's sort on the grid — stored override first, then the declared sort;
   * a view stating no preference keeps the ordering already there (the pre-decision-10-sort
   * behavior, and the least surprising for the tab an operator never sorted). Broadcast only —
   * the caller owns the refetch, because which request carries the new sort differs between the
   * store and storeless paths.
   */
  private applyViewSort(): boolean {
    const target = this.viewSort();
    if (!target) return false;
    if (sortMetaEquals(target, this.table._multiSortMeta ?? [])) return false;

    this.broadcastSort(target);
    return true;
  }

  /**
   * Rewrites the sort inside PrimeNG's persisted state to the one `ngOnInit` resolved. The two
   * can disagree only on a viewed table — PrimeNG's state is table-global while the resolved
   * sort is the view's — and PrimeNG's restoreState() applies its copy on the first [value]
   * change, AFTER the initial request already left with ours: without this line the grid's
   * arrows and every later request silently fall back to the other view's sort. Handed the
   * already-parsed state: `ngOnInit` reads the blob once for the whole init pass.
   */
  private alignPersistedTableStateSort(state: any): void {
    if (!this.views?.length || !this.resolvedStateKey || !state) return;

    const resolved = this.initialMultiSortMeta ?? [];
    if (sortMetaEquals(state.multiSortMeta ?? [], resolved)) return;

    state.multiSortMeta = resolved.length ? resolved : undefined;
    writeStoredJson(this.filterStorage, this.resolvedStateKey, state);
  }

  //#endregion

  /**
   * The DECLARED default in the current context: the active view's own sort when it declares
   * one, else the table-level `defaultSortField`. Every "fall back to the declared default"
   * site — tri-state un-sort, Clear filters, the unsorted-load safety net — reads this, so
   * un-sorting inside a view lands on the view's ordering, not the table's.
   */
  private defaultMultiSortMeta(): SortMeta[] | null {
    return this.viewDeclaredMultiSortMeta() ?? this.tableDefaultMultiSortMeta();
  }

  /**
   * The active view's declared sort, sanitized: a non-sortable field would answer every load
   * with a 400 (`keepSortableMeta`'s rationale), so it is dropped rather than sent; a view
   * whose declaration sanitizes to nothing falls through to the table default.
   */
  private viewDeclaredMultiSortMeta(): SortMeta[] | null {
    return this.sanitizedSortMeta(this.activeView()?.sort);
  }

  /**
   * The table-level declared default as PrimeNG sort meta, or null when none is declared. On a
   * store table it applies even while its column is hidden — the sort chip names the ordering
   * whether or not the column is on screen (decision 2b). A STORELESS table has no chip, so a
   * hidden default would order the grid with nothing on screen naming it; there the default
   * dies with the column, exactly as the legacy invariant had it (see `dropHiddenColumnSort`).
   */
  private tableDefaultMultiSortMeta(): SortMeta[] | null {
    if (!this.defaultSortField) return null;

    const defaultSortCol = this.cols?.find(
      (col) => col.field === this.defaultSortField,
    );
    if (
      defaultSortCol &&
      !this.filters &&
      !this.isColumnVisible(defaultSortCol)
    )
      return null;
    // A declared default on a non-sortable column is a consumer mistake the backend answers
    // with a 400 on every load; fall back to its implicit Id DESC instead (see keepSortableMeta).
    if (defaultSortCol && !this.isColumnSortable(defaultSortCol)) return null;

    return [{ field: this.defaultSortField, order: this.defaultSortOrder }];
  }

  /**
   * `keepSortableMeta` with "sanitized to nothing" surfaced as null — the rule every sort
   * SOURCE shares ("a sort that sanitizes to nothing is no sort"), so the resolution chains
   * can `??` past an empty answer instead of stopping on an empty array.
   */
  private sanitizedSortMeta(
    multiSortMeta: SortMeta[] | null | undefined,
  ): SortMeta[] | null {
    const sortable = this.keepSortableMeta(multiSortMeta);
    return sortable.length ? sortable : null;
  }

  /**
   * Drops sort meta the backend has no sort case for. Persisted state outlives the rule that
   * produced it — a sort stored before a column became non-sortable (or before the library
   * started disabling `*CommaSeparated` headers) would otherwise ride every lazy load, and the
   * generated `Build` answers an unknown sort field with a 400. There would be no way out from
   * the UI either: the header that could clear it is exactly the one no longer clickable.
   * Unknown fields (no matching column) are left alone — a consumer may sort on something it
   * declares no column for.
   */
  private keepSortableMeta(
    multiSortMeta: SortMeta[] | null | undefined,
  ): SortMeta[] {
    return (multiSortMeta ?? []).filter((sortMeta) => {
      const col = this.cols?.find((c) => c.field === sortMeta?.field);
      return col == null || this.isColumnSortable(col);
    });
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

    if (this.table) this.broadcastSort(defaultSort);
  }

  /** Puts `meta` on the grid and tells the header icons — no fetch; the caller owns that. */
  private broadcastSort(meta: SortMeta[]): void {
    this.table._multiSortMeta = meta;
    this.table.tableService.onSort(meta);
  }

  onPageChange(container: HTMLElement): void {
    scrollElementIntoViewIfAboveViewport(container);
  }

  /**
   * The one pending predicate: PrimeNG's overlay and the container's `aria-busy` must never
   * disagree about whether a fetch is in flight.
   *
   * It used to also test `items === undefined`, for the first load. That clause is dead now
   * that `loading` initialises true and every lazyLoad raises it — except in the one state
   * where it was actively wrong: a FAILED first load lowers the flag with `items` still
   * undefined, which pinned the overlay up forever. Same stranded overlay this component was
   * fixed for once already, through the other door.
   */
  get isPending(): boolean {
    return this.loading;
  }

  lazyLoad(event: TableLazyLoadEvent) {
    // Every refetch is pending, not just the first load and reload(). `items` is deliberately
    // left alone: the previous page stays on screen under the overlay (stale-while-revalidate)
    // rather than blanking. Raising the flag is also what stops PrimeNG's empty message, gated
    // on `isEmpty() && !loading`, from claiming "no records" for the length of the request.
    this.loading = true;

    this.applyDefaultSortIfUnsorted(event);
    this.lastLazyLoadEvent = event;
    this.refreshSortKeys();
    this.rangeAnchorId = null;

    let tableFilter: Filter = event as unknown as Filter;
    tableFilter.additionalFilterIdLong = this.additionalFilterIdLong;

    // The store is the ONLY source of what narrows the grid, on every table. Unconditional on
    // purpose: with the header filters gone, the one remaining writer of PrimeNG's own
    // `event.filters` is a stale persisted blob from the header-filter era replayed by
    // `restoreState` — and letting that through would narrow a storeless table invisibly, with
    // no chip and no button left to clear it.
    tableFilter.filters = this.filters?.toFilterPayload() ?? {};

    this.onLazyLoad.next(tableFilter);

    // Issued NOW rather than after the list lands: it needs only `tableFilter`, so serialising
    // it behind the list response cost a second round trip on every page of every
    // selection-enabled table — and the overlay is held up for both. The `.catch` at creation
    // is mandatory, not defensive: when the list request errors, `next` never runs, and a
    // rejected promise nobody awaits is exactly the unowned rejection this handler exists to
    // prevent. The rejection is re-thrown out of `awaitSelectedIds` instead, where it has one.
    const selectedIds =
      this.selectedLazyLoadObservableMethod != null
        ? firstValueFrom(
            this.selectedLazyLoadObservableMethod(tableFilter),
          ).catch((error) => error as Error)
        : null;

    this.getPaginatedListObservableMethod(tableFilter).subscribe({
      next: async (res) => {
        // The await below is INSIDE an async subscriber, so a rejection there settles nothing:
        // `error:` belongs to the paginated-list observable and never runs. That used to strand
        // the overlay forever, hence the finally.
        try {
          this.items = res.data;
          this.totalRecords = res.totalRecords;
          this.onTotalRecordsChange.next(res.totalRecords);

          await this.reconcileSelectionForLoadedPage(selectedIds);
        } catch (error) {
          // The library's own owner: it toasts, it skips HttpErrorResponse (the interceptor
          // already reported those), and a consumer's ErrorHandler wrapper forwards to their
          // tracker. console.error here would have dropped both.
          this.errorHandler.handleError(error);
        } finally {
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  /**
   * Repaints the checkbox column for the page that just landed. Split out of the subscriber so
   * the try/finally that guarantees the pending flag wraps an await, not fifty lines of
   * selection algebra.
   */
  private async reconcileSelectionForLoadedPage(
    selectedIds: Promise<LazyLoadSelectedIdsResult | Error> | null,
  ): Promise<void> {
    if (selectedIds == null) {
      if (this.deleteListFromTableObservableMethod) {
        this.fakeSelectedItems = this.items
          .map((x) => x[this.idField])
          .filter((id) => this.newlySelectedItems.includes(id));
      }
      return;
    }

    const selectedRowsMethodResult = await selectedIds;
    if (selectedRowsMethodResult instanceof Error) throw selectedRowsMethodResult;

    this.currentPageSelectedItemsFromDb = [
      ...selectedRowsMethodResult.selectedIds,
    ];

    if (this.isFirstTimeLazyLoad == true) {
      this.rowsSelectedNumber = selectedRowsMethodResult.totalRecordsSelected;
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

  private loadFormArrayItems() {
    this.items = this.getFormArrayItems(this.additionalIndexes);
    this.rangeAnchorId = null;
    this.items.forEach((item, index) => {
      item.index = index;
    });
  }

  /**
   * Single source for whether a column header offers click-to-sort (drives both
   * `pSortableColumnDisabled` and the sort icon). Beyond the consumer's explicit `sortable: false`,
   * `*CommaSeparated` fields are auto-disabled: the backend's `PaginatedResultGenerator` never emits
   * a sort case for collection columns (the same naming convention decides both), and since unknown
   * sort fields are rejected with a 400, a clickable header here would be a guaranteed error toast.
   */
  isColumnSortable(col: Column): boolean {
    return (
      col.sortable !== false &&
      !!col.field &&
      !col.field.endsWith('CommaSeparated')
    );
  }

  /** The column the header menu is currently open for, and its header cell. */
  menuColumn: Column | null = null;

  private menuHeaderCell: HTMLElement | null = null;

  private draggedColumn: Column | null = null;

  /** False while a gesture that began on the menu chevron or the resize grip is in flight. */
  private headerDragArmed = true;

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

  openColumnMenu(col: Column, event: Event): void {
    this.menuColumn = col;
    // Kept so `fitMenuColumn` can find the column's cells: it needs the header's position among
    // its siblings, which is the only thing that ties a Column to a table column in the DOM.
    this.menuHeaderCell = (event.target as HTMLElement).closest('th');
    this.columnMenu().toggle(event);
  }

  /**
   * Switches to a view. Clears first, always: a view is a STATE rather than an addition, and two
   * of them composed produce a question nobody asked. The clear runs through the store directly
   * rather than through `clear(table)`, which would also wipe the sort and the persisted state a
   * view is entitled to keep.
   */
  selectView(view: TableView): void {
    this.activeViewId = view.id;
    // Always writes — including the first view, so a deliberate "Sve" during a deploy race
    // sticks, and so an explicit pick beats a stored id whose view has not arrived yet
    // (`adoptStoredView` compares against this key).
    this.persistActiveView();

    // The keys just changed under us, so the layout has to be re-read for the view being entered
    // — otherwise the one being left keeps rendering until something else forces a reload.
    this.restoreColumnVisibility();
    this.restoreColumnLayout();
    this.refreshVisibleCols();

    // Sort is view state too (decision 10's sort half). Swapped BEFORE the filter transition so
    // the one request the requery effect sends carries both.
    const sortChanged = this.applyViewSort();

    if (!this.filters) {
      if (sortChanged) this.table.sortMultiple();
      return;
    }

    const appliedBefore = this.filters.applied();

    // The filter key moved with the view too; `enterViewFilters` holds the stored-wins and
    // transient rules.
    this.filters.clear();
    this.enterViewFilters(view, 'click');

    // The requery effect runs only when the applied set actually changed — `clear()` on an
    // already-empty store writes nothing (two views sharing one filter answer are exactly that)
    // — so a sort-only switch fetches here. `applied` is a computed: reference equality across
    // the transition means none of its sources moved.
    if (sortChanged && this.filters.applied() === appliedBefore) {
      this.refetchFromFirstPage();
    }
  }

  /**
   * The filter this column stands for. Defaults to the column's own `field`, because a store id
   * IS a backend property name — `toFilterPayload` emits it straight into `Filter.filters`. So
   * `filterId` is an escape hatch for the rare mismatch, not something every column restates:
   * all eight of the first migrated table's declarations were the field name written a second
   * time.
   */
  private filterIdFor(col: Column): string | undefined {
    return col.filterId ?? col.field;
  }

  /**
   * Asks the STORE, not the column. Checking only that an id was declared let a typo through to
   * `store.get(id)`, which reads `definitions[id].label` — so a mistyped id threw on click rather
   * than greying the item out. With 195 declarations to migrate, that is the failure mode worth
   * closing here rather than in each consumer.
   */
  canFilterColumn(col: Column): boolean {
    const id = this.filterIdFor(col);

    return id != null && this.filters?.definitions[id] != null;
  }

  /**
   * Opens the bar's editor for this column's filter. The bar took the filter off the header and
   * the header is still where an operator reaches for it, which is twenty-seven tables' worth of
   * habit; this is the shortcut back rather than a second filter surface — it drives the same
   * editor, on the same bar, that `+ Filter` opens.
   */
  filterMenuColumn(): void {
    const col = this.menuColumn;
    this.columnMenu().hide();
    if (!col || !this.canFilterColumn(col)) return;

    this.filterBar()?.startEditing(this.filterIdFor(col)!);
  }

  /**
   * Sorts by this column in the NAMED direction. Clicking a header cycles asc, desc and off,
   * which is fine for one column and a guess for a direction someone wants now — you click, look,
   * and click again if it went the other way. This is also the only sort path that works from a
   * keyboard.
   *
   * A column the generated paginator has no sort case for answers every load with a 400, so the
   * items are disabled by the same predicate that keeps its header from being clickable.
   */
  sortMenuColumn(order: 1 | -1): void {
    const col = this.menuColumn;
    this.columnMenu().hide();
    if (!col?.field || !this.isColumnSortable(col)) return;

    this.table._multiSortMeta = [{ field: col.field, order }];
    this.table.sortMultiple();
    this.persistActiveViewSort();
  }

  /**
   * Sizes the column to the widest thing in it. The cheaper escape from a cramped column: no
   * drag, no aim. Unlike a drag it does NOT trade with the neighbour — a minimum that grew is
   * exactly the case decision 4 lets push the table past its container and start scrolling.
   *
   * Measures the CELL CONTENT, not the cell: the clamp lives on the inner span, so a `td` reports
   * a scrollWidth that fits while the text inside it does not.
   */
  fitMenuColumn(): void {
    const col = this.menuColumn;
    const th = this.menuHeaderCell;
    this.columnMenu().hide();
    if (!col?.field || !th?.parentElement) return;

    // Walked up from the header rather than reached through PrimeNG's own ElementRef: the th is
    // the only thing that ties a Column to a table column in the DOM, and it already knows which
    // table it is in.
    const index = Array.from(th.parentElement.children).indexOf(th) + 1;
    const cells = th
      .closest('table')
      ?.querySelectorAll<HTMLElement>(`tbody tr > td:nth-child(${index})`);
    if (!cells?.length) return;

    // Padding comes from one CSS rule for every cell in the column, so it is read once off the
    // first — a getComputedStyle per cell was a hundred style flushes for one number.
    const styles = getComputedStyle(cells[0]);
    const padding =
      (parseFloat(styles.paddingLeft) || 0) +
      (parseFloat(styles.paddingRight) || 0);

    // Cells AND their descendants in one query rather than one per cell: the clamp lives on an
    // inner span, so a td reports a scrollWidth that fits while the text inside it does not, and
    // a consumer's cell template can nest the overflowing element arbitrarily deep.
    const content = th
      .closest('table')!
      .querySelectorAll<HTMLElement>(
        `tbody tr > td:nth-child(${index}), tbody tr > td:nth-child(${index}) *`,
      );

    let widest = th.scrollWidth;
    for (const node of Array.from(content)) {
      widest = Math.max(widest, node.scrollWidth + padding);
    }
    this.columnWidths[col.field] = this.shareThatFits(col, th, widest);
    this.persistColumnLayout();
  }

  /**
   * The share that actually yields `needed` pixels — not `needed` scaled by the column's current
   * px-per-share, which is the obvious answer and the wrong one. Shares are a PROPORTION of
   * whatever width the table has, so growing this column's share also shrinks the realized width
   * of every other column, and the naive figure lands short. The spec caught it as a cell that
   * was still clipped after being told to fit.
   *
   * While the columns fit, the table keeps the container's width and the proportion has to solve
   *   needed / tableWidth = newShare / (otherShares + newShare)
   * with `otherShares` recovered from what this column currently renders at. Once `needed` is the
   * whole container the proportional regime has no answer: the table is about to overrun its
   * container and scroll (decision 4), and there a share IS its own length, so the pixels convert
   * straight through the root font size.
   */
  private shareThatFits(col: Column, th: HTMLElement, needed: number): number {
    const share = this.columnShare(col);
    const thPx = Math.max(th.offsetWidth, 1);
    const tableWidth = (th.closest('table') as HTMLElement | null)?.offsetWidth ?? 0;

    if (needed < tableWidth) {
      const otherShares = (share * (tableWidth - thPx)) / thPx;

      return (needed * otherShares) / (tableWidth - needed);
    }

    return needed / rootFontSizePx();
  }

  hideMenuColumn(): void {
    if (this.menuColumn) this.toggleColumn(this.menuColumn, false);
    this.columnMenu().hide();
  }

  /**
   * Whether this column's cells give up the one-line clamp. The DEFAULT stays clamped
   * (CLAUDE.md -> "Operator-owned view", decision 9); what changes is that the operator picks
   * which column pays the row height, not whoever wrote `cols`.
   *
   * Lands on the td as `.cell-wrap`, whose inherited `--spiderly-cell-*` properties are how the
   * toggle crosses into a projected `spiderlyCellTemplate` too — the contract's one telling is
   * on that SCSS rule; a consumer clamp that hard-codes its values opts out.
   */
  isColumnWrapped(col: Column): boolean {
    return col.field != null && this.columnWrap[col.field] === true;
  }

  get isMenuColumnWrapped(): boolean {
    return this.menuColumn != null && this.isColumnWrapped(this.menuColumn);
  }

  toggleWrapForMenuColumn(): void {
    const col = this.menuColumn;
    this.columnMenu().hide();
    if (!col?.field) return;

    if (this.columnWrap[col.field]) delete this.columnWrap[col.field];
    else this.columnWrap[col.field] = true;

    this.persistColumnLayout();
  }

  /** Exposed for the template: actions columns carry no field and get no menu. */
  isDataColumn(col: Column): boolean {
    return SpiderlyDataTableComponent.isDataColumn(col);
  }

  /**
   * What the grid is ordered by, resolved to COLUMN NAMES for the bar. Read off PrimeNG's live
   * meta rather than mirrored into a field, so it cannot fall out of step with the sort the table
   * is actually applying. A field with no column left (hidden, or removed in a later release)
   * falls back to its own name rather than vanishing from the list.
   */
  /**
   * What the grid is ordered by, resolved to COLUMN NAMES for the bar.
   *
   * A field refreshed per fetch, not a getter: it is bound to the bar's `sort`, which is a SIGNAL
   * input, so a freshly mapped array failed `Object.is` on every change-detection pass and marked
   * the bar's consumers dirty forever — for a value that changes once per request. Same reason
   * `tableStyle` is a field.
   */
  sortKeys: SortKeyLabel[] = [];

  /**
   * Read off the last REQUEST's sort, not `table._multiSortMeta`. On a first load with a declared
   * default that field is still null: `applyDefaultSortIfUnsorted` writes the default onto the
   * lazy-load event, and PrimeNG fills its own meta only once someone clicks a header. Reading the
   * table there showed no sort on exactly the tables that always have one. A field with no column
   * left falls back to its own name rather than vanishing.
   */
  private refreshSortKeys(): void {
    this.sortKeys = (this.lastLazyLoadEvent?.multiSortMeta ?? []).map((key) => ({
      label: this.cols.find((col) => col.field === key.field)?.name ?? key.field,
      descending: key.order === -1,
    }));
  }

  /** This column's share, override first, then its declared width, then the per-type default. */
  columnShare(col: Column): number {
    if (col.field && this.columnWidths[col.field] != null) {
      return this.columnWidths[col.field];
    }

    if (col.width != null) return this.shareOfDeclaredWidth(col);

    return this.defaultShare(col);
  }

  /**
   * A declared width expressed in SHARE units. rem IS the share unit and px converts; any other
   * unit falls back to the per-type default rather than being read as a bare number.
   *
   * `parseFloat` alone shipped here first, so `width: '150px'` resolved to share 150 against
   * neighbours at 12 — one column eating the grid on the first drag or fit. Every declared width
   * in the consumer happens to be rem today, which is exactly why nothing would have caught it.
   */
  private shareOfDeclaredWidth(col: Column): number {
    const declared = col.width!.trim();
    const value = parseFloat(declared);

    if (Number.isNaN(value)) return this.defaultShare(col);
    if (declared.endsWith('rem')) return value;
    if (declared.endsWith('px')) return value / rootFontSizePx();

    return this.defaultShare(col);
  }

  private defaultShare(col: Column): number {
    if (col.filterType) return DEFAULT_COLUMN_WIDTH_REM[col.filterType];

    // What is left declares no filterType: an actions column. It cannot shrink to fit any more,
    // so the icons need a reservation — each sits in a flex row whose gap is set beside them in
    // the template, inside the cell's own padding. 2.5, not 2.2, so the arithmetic stays exact in
    // binary and the string never reads `8.6000000001rem`.
    return 2 + (col.actions?.length ?? 0) * 2.5;
  }

  /**
   * Trades share between the column being dragged and the one on its right, so the table keeps
   * its total: widening a column never starts a horizontal scroll by itself, which only happens
   * once the SUM of the minimums stops fitting. The px delta is converted through the column's
   * own rendered width, which is the only place the share-to-pixel scale is actually known.
   */
  startColumnResize(col: Column, event: MouseEvent, th: HTMLElement): void {
    event.preventDefault();
    event.stopPropagation();

    const neighbour = this.visibleCols[this.visibleCols.indexOf(col) + 1];
    if (!col.field || !neighbour?.field) return;

    const controller = new AbortController();
    this.resizeAbort?.abort();
    this.resizeAbort = controller;

    const startX = event.clientX;
    const startShare = this.columnShare(col);
    const neighbourShare = this.columnShare(neighbour);
    const sharePerPx = startShare / Math.max(th.offsetWidth, 1);
    // A column that reaches zero share disappears with no way back from the header it lost.
    const floor = 2;

    const neighbourCell = th.nextElementSibling as HTMLElement | null;
    let applied = 0;

    const onMove = (move: MouseEvent) => {
      const delta = (move.clientX - startX) * sharePerPx;
      applied = Math.max(
        Math.min(delta, neighbourShare - floor),
        floor - startShare,
      );

      // The preview is written straight to the two header cells. Under the fixed layout their
      // widths are what lay the grid out, so this IS the live drag — and it costs no change
      // detection, where routing it through `columnWidths` ran a full-grid pass per mouse move
      // (~60/sec) to move two cells. The bindings overwrite these on the next pass anyway.
      th.style.width = `${startShare + applied}rem`;
      if (neighbourCell) {
        neighbourCell.style.width = `${neighbourShare - applied}rem`;
      }
    };

    const onUp = () => {
      controller.abort();
      this.resizeAbort = null;

      this.zone.run(() => {
        this.columnWidths[col.field!] = startShare + applied;
        this.columnWidths[neighbour.field!] = neighbourShare - applied;
        this.persistColumnLayout();
      });

      // Sorting hangs off CLICK, which stopPropagation on mousedown never touched — so every
      // resize also reordered the grid. Swallowing the click on the grip is not enough either:
      // on a drag that ends away from it, the click fires on their common ancestor, the th.
      // One capturing listener, once, on the document.
      const swallow = (click: MouseEvent) => {
        click.stopPropagation();
        click.preventDefault();
      };
      document.addEventListener('click', swallow, {
        capture: true,
        once: true,
      });
      // Removed on the next tick rather than left to `once`. The click that follows a mouseup
      // fires synchronously, so this always catches it — while a drag that ends with no click at
      // all (the pointer leaves the window, the operator navigates away) would otherwise leave a
      // listener sitting on the document to eat someone's next click entirely.
      setTimeout(() =>
        document.removeEventListener('click', swallow, { capture: true }),
      );
    };

    // One controller for the pair, aborted by `onUp` AND by ngOnDestroy. Left to `removeEventListener`
    // inside `onUp` alone, navigating away mid-drag stranded `mousemove` on the document — patched
    // by zone.js, so it ticked the app on every mouse move for the rest of the session, with its
    // closure pinning the dead component and the page of rows it captured.
    this.zone.runOutsideAngular(() => {
      document.addEventListener('mousemove', onMove, {
        signal: controller.signal,
      });
      document.addEventListener('mouseup', onUp, { signal: controller.signal });
    });
  }

  /**
   * The column's declared {@link Column.width}, or the default for its filter type.
   *
   * A width, not a minimum: under the fixed layout {@link tableStyle} establishes, this is what
   * the column is sized from, and the table shares its surplus in PROPORTION to these numbers.
   * Columns declaring none would otherwise take an equal share, which throws away what the
   * per-type defaults say — a boolean holds "Da"/"Ne", a text column holds a name.
   */
  getColWidth(col: Column): string {
    const override = col.field ? this.columnWidths[col.field] : undefined;
    if (override != null) return `${override}rem`;

    // A declared width passes through VERBATIM so a consumer keeps % and px; every other branch is
    // a share and renders in rem. Both halves resolve through `columnShare` otherwise, because
    // this chain and that one were written twice and had already drifted apart on the px case.
    if (col.width != null) return col.width;

    return `${this.defaultShare(col)}rem`;
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

  /**
   * Handle row click event. Arbitration between "this click navigates the row" and "this click
   * belongs to a control inside the row" happens HERE, once, rather than in each interactive
   * surface: anything marked `.row-interactive` (the selection cell, the actions column, editable
   * cells, opted-in `onCellClick` cells) owns its own click and never navigates. Per-surface
   * `stopPropagation` used to be the mechanism and was structurally lossy — the actions column
   * never had one, so a Delete click opened the dialog AND navigated away. Mark new interactive
   * cells with the class; don't add another stop.
   */
  onRowClick(row: any, event: MouseEvent): void {
    if (!this.navigateOnRowClick) return;
    if ((event.target as HTMLElement)?.closest?.('.row-interactive')) return;

    this.navigateToDetails(row?.[this.idField]);
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
    this.selectionWidthObserver?.disconnect();
    this.resizeAbort?.abort();
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

  /**
   * A COMMITTED filter re-queries; a draft does not. Re-fetching per keystroke would be the same
   * lie the chip bar exists to prevent, only paid for in requests rather than in credibility.
   *
   * Back to page one first, and off FRESH metadata rather than `reload()`: that one replays the
   * cached lazy-load event, whose `first` was copied when the event was built, so a filter applied
   * from page 3 would keep asking the server to skip 50 rows a narrower result set does not have.
   * The grid comes back empty for a value that is definitely in it.
   *
   * The first run is PrimeNG's: it fires the initial lazy load itself, and re-querying here would
   * make every table load twice.
   */
  private requeryOnAppliedFilters(): void {
    let isFirstRun = true;

    effect(
      () => {
        this.filters!.applied();

        if (isFirstRun) {
          isFirstRun = false;
          return;
        }

        this.persistAppliedFilters();
        this.refetchFromFirstPage();
      },
      { injector: this.injector },
    );
  }

  /**
   * Back to page one, state saved, one fetch off FRESH metadata — the shared tail of the
   * requery effect and `selectView`'s sort-only switch. The save is load-bearing: PrimeNG
   * saves state only from its own interactions, so without it the pre-change page offset
   * survives in storage and the NEXT visit replays it against a narrowed result set — the
   * backend answers data=[] for an offset the filtered total no longer reaches, and the grid
   * renders empty under chips claiming matches.
   */
  private refetchFromFirstPage(): void {
    this.table.first = 0;
    this.table.firstChange.emit(0);
    if (this.table.isStateful()) this.table.saveState();
    this.lazyLoad(this.table.createLazyLoadMetadata());
  }

  reload() {
    // Nothing to replay before the table's own initial load, which is already on its way.
    if (this.lastLazyLoadEvent == null) return;

    // Not special: same refetch as a page flip, so lazyLoad owns both the flag and `items`.
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
      case 'multiselect': {
        // Translate through the column's own option list. Without this the cell shows the stored
        // value, so an enum column reads `1` instead of "Info", and an FK column reads the id.
        //
        // Match on `code`, not `value`: that is the property PrimengOption carries (see the note
        // on the class). The raw value is the fallback rather than an empty cell, because the
        // options are usually filled asynchronously after the first paint — blanking the column
        // until they land would be a worse regression than the number this replaces.
        const multiselectValue = rowData[col.field];
        if (multiselectValue == null) return null;

        const option = col.dropdownOrMultiselectValues?.find(
          (x) => x.code === multiselectValue,
        );
        return option?.label ?? multiselectValue;
      }
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

  /**
   * Clears everything that narrows the grid. With a store supplied it has to reach BOTH — the
   * store holds the constraints and PrimeNG holds the persisted state and sort — or the bar goes
   * empty while a reload brings the old filters back.
   */
  clear(table: Table) {
    this.filters?.clear();

    table.clear();
    table.clearState();
    // `table.clear()` landed the grid on the declared default (`applyDefaultSortIfUnsorted`),
    // so the view's sort override must go with it — or F5 would bring back the sort Clear
    // visibly removed.
    this.persistActiveViewSort();
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

  /**
   * Captures the shift state for the checkbox change this press produces — id-paired so an
   * aborted press can never leak into a later toggle (see CLAUDE.md). Also stops shift+click
   * from extending the browser text selection, which starts at mousedown.
   */
  onSelectionCellMouseDown(event: MouseEvent, id: number) {
    // Arm only for a press on the checkbox itself: a press on the surrounding cell produces no
    // `change`, so arming there would strand the flag until some later toggle consumed it. The
    // text-selection preventDefault still covers the whole cell.
    if ((event.target as HTMLElement).closest('p-checkbox')) {
      this.pendingShift = { shiftKey: event.shiftKey, id };
    }
    if (event.shiftKey) event.preventDefault();
  }

  /**
   * The rows the user can currently see, in display order — PrimeNG's own answer, so it cannot
   * drift from what the table paints (it applies `filteredValue` and the paginator slice, and
   * caps lazy pages at `rows` even when the server overshoots). Resolving a range over raw
   * `items` would sweep filtered-out or off-page rows sitting between two visually adjacent
   * ones. An anchor outside this window misses and the shift-click degrades to a plain toggle.
   */
  private renderedRows(): any[] {
    return this.table?.dataToRender(null) ?? this.items;
  }

  selectRow(id: number, index: number) {
    const shiftRange =
      this.pendingShift?.id === id && this.pendingShift.shiftKey;
    this.pendingShift = null;

    // `null` is the no-anchor sentinel, so it must never resolve to a row: a client-side table
    // can hold unsaved rows whose idField is null, and findIndex would happily match one.
    if (shiftRange && this.rangeAnchorId != null && id != null) {
      const rendered = this.renderedRows();
      const indexOf = (rowId: number) =>
        rendered.findIndex((x) => x[this.idField] === rowId);
      const anchorIndex = indexOf(this.rangeAnchorId);
      const clickedIndex = indexOf(id);

      if (anchorIndex !== -1 && clickedIndex !== -1) {
        // The clicked checkbox's NEW state, applied to the whole range. Our model hasn't been
        // updated for this click yet, so the new state is the negation of what we hold.
        this.applyRange(
          rendered,
          anchorIndex,
          clickedIndex,
          !this.isRowSelected(id),
        );
        this.rangeAnchorId = id;
        return;
      }
    }

    this.toggleRow(id, index);
    this.rangeAnchorId = id;
  }

  /** Single-row toggle — the plain-click path. */
  private toggleRow(id: number, index: number) {
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

  /**
   * Applies `select` to every row between the two positions in `rows` (inclusive), skipping rows
   * already in the target state; each change goes through `toggleRow` so the delta model and
   * per-row events stay identical to manual clicks (see CLAUDE.md). The event payload keeps
   * `row.index` for parity with what the single-click template binding passes.
   */
  private applyRange(
    rows: any[],
    fromIndex: number,
    toIndex: number,
    select: boolean,
  ) {
    const start = Math.min(fromIndex, toIndex);
    const end = Math.max(fromIndex, toIndex);

    for (let i = start; i <= end; i++) {
      const rowId = rows[i][this.idField];
      if (this.isRowSelected(rowId) !== select) {
        this.toggleRow(rowId, rows[i].index);
      }
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

/**
 * A saved question. The bar lets an operator BUILD one; a view is the one they ask every morning,
 * asked in a click — and without them the whole column-and-filter rework is a pile of knobs
 * someone re-sets daily (CLAUDE.md -> "Operator-owned view", decision 10).
 *
 * Declared in consumer code, not stored per user: with a handful of operators and a handful of
 * fixed daily jobs, shared built-in views cover the work, while personal saved views would drift
 * into five variants of the same question. Personal LAYOUT choices still persist on top, per view.
 */
export interface TableView<TFilters = any> {
  id: string;
  label: string;
  /**
   * Sets the filters this view asks for. Receives the table's own store, so a consumer writes it
   * with the store's typing. Called on a CLEARED store: a view is a state, not an addition.
   */
  apply?: (filters: TFilters) => void;
  /**
   * A view whose `apply` is a function of NOW — "Danas primljene", "this week". Selecting it
   * always re-runs `apply` instead of restoring what an earlier visit stored: under the normal
   * stored-wins rule a relative-date view clicked yesterday would restore yesterday's date under
   * a tab claiming "today". Layout (columns, widths, wrap) still persists per view as usual —
   * transience is about the QUESTION, not the furniture.
   */
  transient?: boolean;
  /**
   * The view's declared ordering — for the view whose question includes one ("oldest shipped
   * first" IS the work list, not a reading preference). Entering the view applies it; the
   * tri-state un-sort and Clear filters return to it instead of the table default; an operator's
   * own sort overrides it, persisted per view on store tables (decision 10's sort half). A view
   * declaring none keeps whatever ordering the grid already has, exactly as before this field
   * existed.
   */
  sort?: SortMeta[];
}

export class Column<T = any> {
  name?: string;
  field?: string & keyof T;
  /**
   * The column's VALUE SHAPE: it sizes the column (`getColWidth`), picks the cell rendering
   * (`getRowData`, the blob branch) and the numeric right-align. The name is a fossil of the
   * header-filter era — filtering lives in the store handed to `[filters]` — kept because
   * renaming it would be a 195-declaration migration for zero behavior.
   */
  filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
  /**
   * The id of the filter in the table's store that this column stands for, which is what the
   * header menu's `Filter…` opens. A LINK, not a declaration: the filter exists in the store
   * whether or not any column names it, and a column needs this only to offer the shortcut.
   *
   * Untyped against the store on purpose — a `Column<T>` knows its row type and nothing about
   * which store it will be rendered beside, so the compile-time key check lives where the store
   * is declared (`createFilterStore`), not here.
   */
  filterId?: string;
  /**
   * @deprecated Dead since the header-filter deletion: NOTHING reads it. It survives on the type
   * only so details-table cols emitted by an OLDER Spiderly.SourceGenerators (which wrote
   * `showMatchModes: true`) still compile against this library during a mixed-version window —
   * a consumer on published NuGet generators plus an npm-linked library build. Delete it once
   * every consumer regenerates with a generator from this release or later (spiderly#407).
   */
  showMatchModes?: boolean;
  /**
   * @deprecated The same version-skew bridge as {@link showMatchModes}, and nothing else: older
   * generators emit `filterField: 'xxxId'` for every dropdown-typed details-table column, so
   * deleting it breaks the mixed window the moment a consumer adds one. NOTHING reads it —
   * filter ids live in the store, linked from a column via {@link filterId}. Delete with
   * `showMatchModes` (spiderly#407).
   */
  filterField?: string & keyof T;
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
   * CSS length fixing this column's width (e.g. `'8rem'`). Overrides the per-type default,
   * which is generous for a column of short values.
   *
   * Declare a width on the columns that must stay NARROW and leave the flexible one undeclared:
   * the table distributes its surplus in proportion to the declared widths, so a column carrying
   * the long values wants the larger number, not a cap.
   */
  width?: string;
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
    filterType,
    filterId,
    showMatchModes,
    filterField,
    dropdownOrMultiselectValues,
    actions,
    editable,
    showTime,
    decimalPlaces,
    sortable,
    visible,
    lockVisible,
    width,
    onCellClick,
  }: {
    name?: string;
    field?: string & keyof T;
    filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
    filterId?: string;
    showMatchModes?: boolean;
    filterField?: string & keyof T;
    dropdownOrMultiselectValues?: PrimengOption[];
    actions?: Action[];
    editable?: boolean;
    showTime?: boolean;
    decimalPlaces?: number;
    sortable?: boolean;
    visible?: boolean;
    lockVisible?: boolean;
    width?: string;
    onCellClick?: (event: CellClickEvent) => void;
  } = {}) {
    this.name = name;
    this.field = field;
    this.filterType = filterType;
    this.filterId = filterId;
    this.showMatchModes = showMatchModes;
    this.filterField = filterField;
    this.dropdownOrMultiselectValues = dropdownOrMultiselectValues;
    this.actions = actions;
    this.editable = editable;
    this.showTime = showTime;
    this.decimalPlaces = decimalPlaces;
    this.sortable = sortable;
    this.visible = visible;
    this.lockVisible = lockVisible;
    this.width = width;
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
