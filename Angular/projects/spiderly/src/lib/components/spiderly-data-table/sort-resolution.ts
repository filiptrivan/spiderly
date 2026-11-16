import { SortMeta } from 'primeng/api';
import type { ViewScope } from './active-view';
import { ColumnLayout } from './column-layout';
import { Column } from './data-table-column';
import type { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { TableStateStorage } from './table-state-storage';

/** Order matters: `[a, b]` and `[b, a]` ask the server for different orderings. */
export function sortMetaEquals(a: SortMeta[], b: SortMeta[]): boolean {
  return (
    a.length === b.length &&
    a.every((m, i) => m.field === b[i].field && m.order === b[i].order)
  );
}

/**
 * Single source for whether a column header offers click-to-sort (drives both
 * `pSortableColumnDisabled` and the sort icon). Beyond the consumer's explicit `sortable: false`,
 * `*CommaSeparated` fields are auto-disabled: the backend's `PaginatedResultGenerator` never emits
 * a sort case for collection columns (the same naming convention decides both), and since unknown
 * sort fields are rejected with a 400, a clickable header here would be a guaranteed error toast.
 */
export function isColumnSortable(col: Column): boolean {
  return (
    col.sortable !== false &&
    !!col.field &&
    !col.field.endsWith('CommaSeparated')
  );
}

/** The table's inputs that decide which sort applies. */
export type SortResolutionHost = Pick<
  SpiderlyDataTableComponent,
  'cols' | 'filters' | 'views' | 'defaultSortField' | 'defaultSortOrder'
>;

/**
 * Which sort a `spiderly-data-table` applies. The operator's stored override for the active view
 * comes first, then the view's declared sort, then the table default. Each loses the fields that
 * the backend cannot sort. A null answer means "unsorted".
 */
export class SortResolution {
  constructor(
    private readonly host: SortResolutionHost,
    private readonly storage: TableStateStorage,
    private readonly scope: ViewScope,
    private readonly layout: ColumnLayout,
  ) {}

  /**
   * The sort the FIRST request carries, bound to p-table's [multiSortMeta]. On a viewed table the
   * persisted table-global sort is a NON-SOURCE (2026-09-06): sort is per-view state (override ??
   * declared ?? table default), and the blob — which holds the last ordering ANY view used — would
   * re-impose across views the exact carry-over the per-view model removes, on every F5. It still
   * carries rows/first there, and stays the one sort memory of a view-less table. We read it all
   * ourselves because PrimeNG's restoreState() runs on the first [value] change — after the
   * initial lazy emit has already left — and is then told to agree (see
   * `alignPersistedTableStateSort`).
   */
  initialMultiSortMeta(tableState: any): SortMeta[] | null {
    return this.host.views?.length
      ? this.resolvedViewSort()
      : (this.sanitizedSortMeta(tableState?.multiSortMeta) ??
          this.tableDefaultMultiSortMeta());
  }

  /**
   * Rewrites the sort inside PrimeNG's persisted state to the one `ngOnInit` resolved. The two
   * can disagree only on a viewed table — PrimeNG's state is table-global while the resolved
   * sort is the view's — and PrimeNG's restoreState() applies its copy on the first [value]
   * change, AFTER the initial request already left with ours: without this line the grid's
   * arrows and every later request silently fall back to the other view's sort. Handed the
   * already-parsed state: `ngOnInit` reads the blob once for the whole init pass.
   */
  alignPersistedTableStateSort(state: any, resolved: SortMeta[]): void {
    if (!this.host.views?.length || !this.storage.persists || !state) return;

    if (sortMetaEquals(state.multiSortMeta ?? [], resolved)) return;

    state.multiSortMeta = resolved.length ? resolved : undefined;
    this.storage.writeTableState(state);
  }

  /**
   * THE resolution ladder, named once: the operator's stored override, else the declared
   * default (`defaultMultiSortMeta` = view's declared sort ?? table default). Null means
   * "unsorted" — callers append their own `?? []` / blob terms rather than re-spelling the
   * ladder, so a future term (a per-view default refinement, decision 10's columns half)
   * lands everywhere at once.
   */
  resolvedViewSort(): SortMeta[] | null {
    return this.persistedViewSort() ?? this.defaultMultiSortMeta();
  }

  /**
   * Records `current` as the active view's override — called from the EXPLICIT sort
   * gestures only (header click, `applySort` — the menu and both bar entry points), never
   * from a load: a sort merely inherited at init must not crystallize into an override, or a
   * later change to the view's declared sort would stop flowing to operators who never
   * touched sort — the same flows-through rule `columnVisibilityOverrides` documents. A sort
   * equal to the declared default is stored as ABSENCE for the same reason.
   */
  persistActiveViewSort(current: SortMeta[]): void {
    if (!this.keepsViewSort) return;

    this.storage.write(
      'sort',
      this.scope.id,
      sortMetaEquals(current, this.defaultMultiSortMeta() ?? []) ? null : current,
    );
  }

  /**
   * The DECLARED default in the current context: the active view's own sort when it declares
   * one, else the table-level `defaultSortField`. Every "fall back to the declared default"
   * site — tri-state un-sort, Clear filters, the unsorted-load safety net — reads this, so
   * un-sorting inside a view lands on the view's ordering, not the table's.
   */
  defaultMultiSortMeta(): SortMeta[] | null {
    return this.viewDeclaredMultiSortMeta() ?? this.tableDefaultMultiSortMeta();
  }

  /**
   * Whether the operator's per-view sort override persists. Not on a table without views — those
   * keep the pure PrimeNG sort persistence they always had — and not on a STORELESS table: there
   * "hidden contributes nothing" retires a hidden column's sort (`dropHiddenColumnSort`) with no
   * persist call at the hide site, so an override restored blind at the next init would re-impose
   * a sort no chip can name. Store tables — where every viewed consumer lives — have the chip.
   */
  private get keepsViewSort(): boolean {
    return !!this.host.views?.length && !!this.host.filters;
  }

  /** The operator's stored sort for the active view, sanitized like any persisted sort. */
  private persistedViewSort(): SortMeta[] | null {
    if (!this.keepsViewSort) return null;

    const stored = this.storage.read('sort', this.scope.id);
    return Array.isArray(stored) ? this.sanitizedSortMeta(stored) : null;
  }

  /**
   * The active view's declared sort, sanitized: a non-sortable field would answer every load
   * with a 400 (`keepSortableMeta`'s rationale), so it is dropped rather than sent; a view
   * whose declaration sanitizes to nothing falls through to the table default.
   */
  private viewDeclaredMultiSortMeta(): SortMeta[] | null {
    return this.sanitizedSortMeta(this.scope.view()?.sort);
  }

  /**
   * The table-level declared default as PrimeNG sort meta, or null when none is declared. On a
   * store table it applies even while its column is hidden — the sort chip names the ordering
   * whether or not the column is on screen (decision 2b). A STORELESS table has no chip, so a
   * hidden default would order the grid with nothing on screen naming it; there the default
   * dies with the column, exactly as the legacy invariant had it (see `dropHiddenColumnSort`).
   */
  private tableDefaultMultiSortMeta(): SortMeta[] | null {
    if (!this.host.defaultSortField) return null;

    const defaultSortCol = this.host.cols?.find(
      (col) => col.field === this.host.defaultSortField,
    );
    if (
      defaultSortCol &&
      !this.host.filters &&
      !this.layout.isColumnVisible(defaultSortCol)
    )
      return null;
    // A declared default on a non-sortable column is a consumer mistake the backend answers
    // with a 400 on every load; fall back to its implicit Id DESC instead (see keepSortableMeta).
    if (defaultSortCol && !isColumnSortable(defaultSortCol)) return null;

    return [
      { field: this.host.defaultSortField, order: this.host.defaultSortOrder },
    ];
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
      const col = this.host.cols?.find((c) => c.field === sortMeta?.field);
      return col == null || isColumnSortable(col);
    });
  }
}
