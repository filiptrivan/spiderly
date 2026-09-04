import { CommonModule, formatDate, formatNumber } from '@angular/common';
import {
  AfterViewInit,
  Component,
  ContentChild,
  ContentChildren,
  ErrorHandler,
  EventEmitter,
  Inject,
  Input,
  LOCALE_ID,
  OnChanges,
  OnDestroy,
  OnInit,
  Output,
  QueryList,
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
import {
  CellTemplateContext,
  SpiderlyCellTemplateDirective,
} from '../../directives/spiderly-cell-template.directive';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { Filter } from '../../entities/filter';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { PaginatedResult } from '../../entities/paginated-result';
import { PrimengOption } from '../../entities/primeng-option';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import {
  FilterBarSource,
  SpiderlyFilterBarComponent,
} from '../../filters/spiderly-filter-bar.component';
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
 * Default column width per filter type, in rem. Sized for the HEADER — the filter input plus its
 * match-mode dropdown — so a column of short values reserves more than its content needs; that is
 * deliberate, and {@link Column.width} is the per-column override.
 *
 * A table, not a switch, so the mapping is exhaustive: a new `filterType` fails the build here
 * instead of silently inheriting the actions-column reservation below.
 */
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
    MultiSelectModule,
    PopoverModule,
    DatePickerModule,
    CheckboxModule,
    SpiderlyFilterBarComponent,
    TooltipModule,
  ],
})
export class SpiderlyDataTableComponent
  implements OnInit, OnChanges, AfterViewInit, OnDestroy
{
  private readonly destroy$ = new Subject<void>();

  @ViewChild('dt') table: Table;

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
   * The consumer's filter store. Supplying it switches this table's filter surface to the chip
   * bar and takes `p-columnFilter` out of the header; supplying nothing keeps the legacy
   * `Column.filterType` header filters. The SHAPE of the input is the switch, deliberately — see
   * CLAUDE.md -> "Operator-owned view", decision 2 — so consumers migrate one table at a time and
   * the legacy path is deletable once nothing passes the old shape.
   */
  @Input() filters?: FilterBarSource;
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

  matchModeDateOptions: SelectItem[] = [];
  matchModeNumberOptions: SelectItem[] = [];
  matchModeTextOptions: SelectItem[] = [];
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
        return;
      }

      originalSort(event);
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

  ngOnChanges(): void {
    // Only meaningful once ngOnInit has resolved the defaults; before that it is a no-op re-run.
    if (this.rows != null) this.mergeActivePageSizesIntoOptions();
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

    // Only the three the generated paginator implements for strings
    // (PaginatedResultGenerator.GetCaseForString). Declared rather than left to PrimeNG,
    // whose own text list adds notContains/endsWith/notEquals — modes the backend answers
    // with InvalidMatchMode, i.e. a 400 on every load once the user picks one.
    this.matchModeTextOptions = [
      {
        label: this.translocoService.translate('StartsWith'),
        value: MatchModeCodes.StartsWith,
      },
      {
        label: this.translocoService.translate('Contains'),
        value: MatchModeCodes.Contains,
      },
      {
        label: this.translocoService.translate('Equals'),
        value: MatchModeCodes.Equals,
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

    this.mergeActivePageSizesIntoOptions();

    this.restoreColumnVisibility();
    this.reconcileVisibilityWithPersistedConstraints();
    this.reconcilePersistedMatchModes();
    // Restored filters are applied by definition — mark them before the first paint rather
    // than waiting for the first response, which is the reload case the icon exists for.
    this.snapshotAppliedFilters(this.persistedTableState()?.filters);
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
      if (this.columnHasConstraint(this.table, col)) {
        delete this.table.filters[this.filterKey(col)];
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

  /**
   * The one home of the filter-state key rule (sort meta is keyed by `field` instead).
   * Instance rather than static so the template's `[field]` binding can share it.
   */
  filterKey(col: Column): string | undefined {
    return col.filterField ?? col.field;
  }

  /**
   * The filter keys the rows on screen were actually narrowed by. Kept apart from
   * `table.filters` because those are not the same question: PrimeNG's `onModelChange`
   * writes each keystroke of a text/numeric filter straight into the meta and only calls
   * `_filter()` for the auto-applying types, so `table.filters` holds PENDING edits too.
   * Reading it for the header icon marked a column filtered the moment the operator typed
   * a character, before Apply — claiming the grid was narrowed when it was not.
   */
  private appliedFilterKeys = new Set<string>();

  /**
   * Whether the rows on screen are narrowed by this column — feeds the projected
   * `filtericon` template. Why not the template's `let-hasFilter`, and why applied rather
   * than pending state: CLAUDE.md → "Active-filter header icon".
   */
  isColumnFiltered(col: Column): boolean {
    return this.appliedFilterKeys.has(this.filterKey(col));
  }

  /** Whether the column carries a constraint at all, applied or still being typed. */
  private columnHasConstraint(table: Table, col: Column): boolean {
    return SpiderlyDataTableComponent.isActiveFilterMeta(
      table.filters?.[this.filterKey(col)],
    );
  }

  /**
   * Records what a just-applied filter set narrows by. Called from every path that commits
   * one: `(onFilter)` (Apply, auto-apply, a per-column Clear), `lazyLoad` (which also covers
   * `table.clear()` — it re-queries WITHOUT emitting onFilter), the caption's Clear filters,
   * and once from `ngOnInit` off persisted state, so a restored filter is marked on first
   * paint rather than after the first response.
   */
  private snapshotAppliedFilters(filters: any): void {
    const applied = new Set<string>();

    for (const [field, meta] of Object.entries(filters ?? {})) {
      if (SpiderlyDataTableComponent.isActiveFilterMeta(meta)) applied.add(field);
    }

    this.appliedFilterKeys = applied;
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
      if (constrained.has(this.filterKey(col)) || constrained.has(col.field)) {
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
    // A declared default on a non-sortable column is a consumer mistake the backend answers
    // with a 400 on every load; fall back to its implicit Id DESC instead (see keepSortableMeta).
    if (defaultSortCol && !this.isColumnSortable(defaultSortCol)) return null;

    return [{ field: this.defaultSortField, order: this.defaultSortOrder }];
  }

  private persistedMultiSortMeta(): SortMeta[] | null {
    const state = this.persistedTableState();
    const sortable = this.keepSortableMeta(state?.multiSortMeta);
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

  /**
   * The filter twin of `keepSortableMeta` — persisted state outlives the rule that produced
   * it, here a match mode stored before the column declared `matchModes`. It has to REWRITE
   * storage rather than filter a value on the way past: `ColumnFilter.ngOnInit` skips
   * `initFieldFilterConstraint()` whenever the field already carries a constraint, so our
   * `[matchMode]` never applies to a restored one. Left alone, the column keeps filtering by
   * a mode it no longer offers while its match-mode `<p-select>` renders blank (the stored
   * value is not among the options). Runs before PrimeNG's `restoreState()`, which reads this
   * same key on the first `[value]` change.
   *
   * Deliberately narrow: it touches only constraints whose mode the declaring column no
   * longer offers, so every other byte of the persisted blob survives.
   */
  private reconcilePersistedMatchModes(): void {
    if (!this.resolvedStateKey) return;

    const state = this.persistedTableState();
    if (!state?.filters) return;

    let changed = false;
    for (const col of this.cols ?? []) {
      if (!col.matchModes?.length) continue;

      const offered = this.getColMatchModeOptions(col);
      const meta = state.filters[this.filterKey(col)];
      if (!offered?.length || !Array.isArray(meta)) continue;

      for (const constraint of meta) {
        if (constraint?.matchMode == null) continue;
        if (offered.some((option) => option.value === constraint.matchMode))
          continue;

        constraint.matchMode = this.getColMatchMode(col);
        changed = true;
      }
    }

    if (!changed) return;

    const storage =
      this.stateStorage === 'local' ? localStorage : sessionStorage;
    storage.setItem(this.resolvedStateKey, JSON.stringify(state));
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
    this.rangeAnchorId = null;
    this.snapshotAppliedFilters(event.filters);

    let tableFilter: Filter = event as unknown as Filter;
    tableFilter.additionalFilterIdLong = this.additionalFilterIdLong;

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

  private clientFilterCount = 0;

  filter(event: TableFilterEvent) {
    // Fires from _filter(), i.e. only once a filter is COMMITTED — never on a keystroke.
    this.snapshotAppliedFilters(event.filters ?? this.table?.filters);

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

  /**
   * The column's declared {@link Column.width}, or the default for its filter type.
   *
   * A width, not a minimum: under the fixed layout {@link tableStyle} establishes, this is what
   * the column is sized from, and the table shares its surplus in PROPORTION to these numbers.
   * Columns declaring none would otherwise take an equal share, which throws away what the
   * per-type defaults say — a boolean holds "Da"/"Ne", a text column holds a name.
   */
  /**
   * The legacy header filter, rendered only while no store is supplied. Deleted with the rest of
   * that path once no consumer passes the old shape.
   */
  showHeaderFilter(col: Column): boolean {
    return (
      !this.filters && col.filterType != null && col.filterType !== 'blob'
    );
  }

  getColWidth(col: Column): string {
    if (col.width != null) return col.width;

    if (col.filterType) return `${DEFAULT_COLUMN_WIDTH_REM[col.filterType]}rem`;

    // What is left declares no filterType: an actions column. It cannot shrink to fit any more,
    // so the icons need a reservation — each sits in a flex row whose gap is set beside them in
    // the template, inside the cell's own padding. 2.5, not 2.2, so the arithmetic stays exact in
    // binary and the string never reads `8.6000000001rem`.
    return `${2 + (col.actions?.length ?? 0) * 2.5}rem`;
  }

  /**
   * Memo for the resolution below. Two jobs: the narrowing maps to a NEW array and a
   * template binding must not hand PrimeNG a fresh reference every CD pass, and both
   * halves (offered list, default mode) must come from ONE computation so they cannot
   * disagree. Keyed on the declared array's identity, so reassigning `col.matchModes`
   * recomputes — note PrimeNG itself reads `matchModeOptions` only in its own `ngOnInit`,
   * so a post-init change still never reaches the rendered dropdown.
   */
  private matchModeResolutions = new WeakMap<
    Column,
    { source: MatchModeCodes[] | undefined; resolution: ResolvedMatchModes }
  >();

  getColMatchModeOptions(col: Column): SelectItem[] | null {
    return this.resolveMatchModes(col).options;
  }

  getColMatchMode(col: Column): any {
    return this.resolveMatchModes(col).defaultMode;
  }

  private resolveMatchModes(col: Column): ResolvedMatchModes {
    const cached = this.matchModeResolutions.get(col);
    if (cached && cached.source === col.matchModes) return cached.resolution;

    const resolution = this.computeMatchModes(col);
    this.matchModeResolutions.set(col, {
      source: col.matchModes,
      resolution,
    });
    return resolution;
  }

  /**
   * Applies `Column.matchModes`, and refuses to half-apply it. Both ways a declaration can
   * be wrong are consumer mistakes that PrimeNG would otherwise turn into a broken filter
   * rather than an error: it reads `matchModeOptions || <type defaults>`, so handing it
   * `[]` renders an EMPTY dropdown (an empty array is truthy) while the default mode still
   * seeds the constraint, and handing it `null` silently restores PrimeNG's own list. So an
   * unusable narrowing falls back to the full list and says so, instead of shipping a
   * dropdown the user cannot pick from.
   */
  private computeMatchModes(col: Column): ResolvedMatchModes {
    const options = this.matchModeOptionsForType(col.filterType);
    const typeDefault = this.defaultMatchModeForType(col.filterType);
    const declared = col.matchModes;

    if (!declared?.length) return { options, defaultMode: typeDefault };

    if (!options?.length) {
      console.error(
        `spiderly-data-table: column "${col.field}" declares matchModes, but filterType "${col.filterType}" has no match modes to narrow — ignoring.`,
      );
      return { options, defaultMode: typeDefault };
    }

    const narrowed = declared
      .map((code) => options.find((option) => option.value === code))
      .filter((option): option is SelectItem => option != null);

    if (narrowed.length !== declared.length) {
      const unsupported = declared.filter(
        (code) => !options.some((option) => option.value === code),
      );
      console.error(
        `spiderly-data-table: column "${col.field}" declares match mode(s) [${unsupported.join(', ')}] that filterType "${col.filterType}" does not support — ignoring them.`,
      );
    }

    if (!narrowed.length) return { options, defaultMode: typeDefault };

    return { options: narrowed, defaultMode: narrowed[0].value };
  }

  private matchModeOptionsForType(
    filterType: string | undefined,
  ): SelectItem[] | null {
    switch (filterType) {
      case 'text':
        return this.matchModeTextOptions;
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

  private defaultMatchModeForType(filterType: string | undefined): any {
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

  /**
   * Whether the filter type applies on every value change, which hides the menu's Apply
   * button — an Apply there would promise a pending state that cannot exist. Boolean
   * auto-applies through PrimeNG's own `onModelChange`; date and dropdown/multiselect
   * through the filter templates THIS component projects, which call `filterCallback`
   * directly (PrimeNG's built-in element, and with it its `onModelChange`, is only
   * rendered when no template is projected — so the date path is ours, not PrimeNG's).
   * With showApplyButton off, PrimeNG also auto-applies match-mode, operator and
   * constraint-removal changes, so the whole menu stays consistent. Typed input
   * (text/numeric) is deliberately NOT here — it commits on Enter/Apply, since applying
   * per keystroke would fire a lazy load per key. Listing the auto types (not the typed
   * ones) is the safe polarity: an unlisted future type keeps its Apply button rather
   * than silently losing its commit.
   *
   * Known gap this does not close, and it predates the flag: the projected date template
   * binds `[ngModel]` one-way and commits on `(onSelect)`, which the datepicker raises for
   * a calendar pick but not for `onUserInput` — so a date TYPED into the field never
   * reaches the constraint at all, with or without an Apply button.
   */
  filterAppliesOnChange(filterType: string): boolean {
    return (
      filterType === 'boolean' ||
      filterType === 'date' ||
      this.isDropOrMulti(filterType)
    );
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
        // Translate through the column's own option list — the same list its filter dropdown
        // already renders labels from. Without this the cell shows the stored value, so an enum
        // column reads `1` while its filter offers "Info", and an FK column reads the id.
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

  clear(table: Table) {
    table.clear();
    table.clearState();
    // clear() re-queries without emitting (onFilter), so the icons would keep their fill
    // on a client-side table; on a lazy one lazyLoad already covers it.
    this.snapshotAppliedFilters(table.filters);
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

/** One column's resolved match-mode config — see `computeMatchModes`. */
interface ResolvedMatchModes {
  /** What the filter menu offers; `null` hands PrimeNG its own list for the type. */
  options: SelectItem[] | null;
  /** The mode a fresh constraint on this column starts with. */
  defaultMode: any;
}

export class Column<T = any> {
  name?: string;
  field?: string & keyof T;
  filterField?: string & keyof T; // Made specificaly for multiautocomplete, maybe for something more in the future
  filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
  filterPlaceholder?: string;
  showMatchModes?: boolean;
  /**
   * Narrows the match modes this column's filter menu offers. Declaration order is display
   * order, and the FIRST entry becomes the column's default match mode. Omit for the filter
   * type's full list and standard default. Example — a datetime column where an exact-equals
   * match can never hit: `matchModes: [MatchModeCodes.GreaterThan, MatchModeCodes.LessThan]`.
   *
   * Read at declaration time: PrimeNG generates the dropdown once in its own `ngOnInit`, so
   * reassigning this later never reaches the rendered menu.
   *
   * The OFFERED list needs `showMatchModes: true` to be visible, but the default mode applies
   * either way — declaring `matchModes` on a column with no picker is the supported way to
   * change just that column's default. Only modes the filter type actually has are honored;
   * anything else is logged and ignored (`computeMatchModes`).
   */
  matchModes?: MatchModeCodes[];
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
   * CSS length fixing this column's width (e.g. `'8rem'`). Overrides the per-filter-type default,
   * which is sized for the HEADER — filter input plus match-mode dropdown — and so is generous
   * for a column of short values.
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
    filterField,
    filterType,
    filterPlaceholder,
    showMatchModes,
    matchModes,
    showAddButton,
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
    filterField?: string & keyof T; // Made specificaly for multiautocomplete, maybe for something more in the future;
    filterType?: 'text' | 'date' | 'multiselect' | 'boolean' | 'numeric' | 'blob';
    filterPlaceholder?: string;
    showMatchModes?: boolean;
    matchModes?: MatchModeCodes[];
    showAddButton?: boolean;
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
    this.filterField = filterField;
    this.filterType = filterType;
    this.filterPlaceholder = filterPlaceholder;
    this.showMatchModes = showMatchModes;
    this.matchModes = matchModes;
    this.showAddButton = showAddButton;
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
