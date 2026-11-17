import { inject } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { SortMeta } from 'primeng/api';
import { Table, TableLazyLoadEvent } from 'primeng/table';
import { SortKeyLabel, SortPickOption } from '../../filters/filter-store';
import { firstCharToUpper } from '../../services/helper-functions';
import { Column } from './data-table-column';
import { isColumnSortable, SortResolution, sortMetaEquals } from './sort-resolution';
import type { SpiderlyDataTableComponent } from './spiderly-data-table.component';

/** The table's members that the sort drives and reads. */
export type TableSortHost = Pick<SpiderlyDataTableComponent, 'table' | 'cols' | 'filters'>;

/**
 * The sort on a `spiderly-data-table`'s PrimeNG grid. It holds each sort gesture, the sort that
 * entering a view puts on the grid, and the labels that the filter bar shows. `SortResolution`
 * decides which sort applies. Construct it in the table's injection context, for `TranslocoService`.
 */
export class TableSort {
  private readonly translocoService = inject(TranslocoService);

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
   * Gates the picker's reset row. Refreshed with `sortKeys` — same request, same reason it is a
   * field rather than a getter. "Default" is view-aware (`defaultMultiSortMeta`), so inside a
   * view the row offers the view's ordering back, not the table's.
   */
  sortIsDefault = true;

  /**
   * What the bar's picker offers: every sortable data column, hidden ones INCLUDED — the picker
   * exists so an ordering does not need its column on screen. Built once in `ngOnInit` (columns
   * are a declaration, not state) and labelled here because the bar knows nothing about columns.
   */
  sortPickOptions: SortPickOption[] = [];

  constructor(
    private readonly host: TableSortHost,
    private readonly resolution: SortResolution,
  ) {}

  private get table(): Table {
    return this.host.table;
  }

  /** Labels the picker's options, from the chooser's list: "every data column, hidden included". */
  setPickOptions(chooserCols: Column[]): void {
    // From chooserCols, not cols: same "every data column, hidden included" set the chooser
    // offers — one derivation of that list, so the picker cannot drift from it.
    this.sortPickOptions = chooserCols
      .filter((col) => isColumnSortable(col))
      .map((col) => ({ field: col.field!, label: col.name }));
  }

  // PrimeNG v19 removed the removableSort property. This overrides the table's
  // sort() method to add tri-state cycling: ascending → descending → unsorted
  // (or back to the declared default sort when one is set).
  setupRemovableSort(): void {
    const originalSort = this.table.sort.bind(this.table);

    this.table.sort = (event: { originalEvent: Event; field: string }) => {
      const sortMeta = this.table.getSortMeta(event.field);

      if (sortMeta && sortMeta.order === -1) {
        const mouseEvent = event.originalEvent as MouseEvent;
        const isMultiSortClick = mouseEvent.metaKey || mouseEvent.ctrlKey;

        if (isMultiSortClick) {
          // Removing ONE level of a multi-sort keeps the reader's place (PrimeNG's metaKey
          // path never pages either), so this cannot ride applySort, whose page reset is
          // half the point. The substituted default keeps sortMultiple() to one emission.
          const remaining = this.table._multiSortMeta.filter(
            (m) => m.field !== event.field,
          );
          this.table._multiSortMeta = remaining.length
            ? remaining
            : (this.resolution.defaultMultiSortMeta() ?? remaining);

          this.table.sortMultiple();
          if (this.table.isStateful()) this.table.saveState();
          this.table.anchorRowIndex = null;
          this.recordOverride();
        } else {
          // A single-click un-sort IS "back to the default" — the exact gesture the bar's
          // reset row asks for, so it uses the same body.
          this.resetSortFromBar();
        }
      } else {
        originalSort(event);
        // PrimeNG's sort() pages/saves itself; the override slot is ours to record.
        this.recordOverride();
      }
    };
  }

  /**
   * The one body of an explicit sort GESTURE — what the doc of
   * `SortResolution.persistActiveViewSort` calls "explicit gestures only": page one, meta on the grid, fetch, save, selection anchor
   * invalidated, override recorded. Shared by the header's single-click un-sort, the header
   * menu and both bar entry points; `broadcastSort` is its deliberate no-fetch sibling. The
   * page reset, `saveState` and anchor line mirror PrimeNG's own `sort()` (`sortMultiple()`
   * does none of the three); the "lands on page one" / "survives a reload" specs pin why.
   */
  applySort(meta: SortMeta[]): void {
    if (this.table.resetPageOnSort) {
      this.table._first = 0;
      this.table.firstChange.emit(0);
    }

    this.table._multiSortMeta = meta;
    this.table.sortMultiple();

    if (this.table.isStateful()) this.table.saveState();
    this.table.anchorRowIndex = null;
    this.recordOverride();
  }

  /** The picker's ask: one sort, replacing the whole meta — the same contract as the header menu. */
  sortFromBar(pick: SortMeta): void {
    this.applySort([pick]);
  }

  /**
   * Back to the resolved default — the view's declared sort, else the table default, else
   * unsorted. Persisting right after is what empties the override slot: a sort equal to the
   * default is stored as absence, so the declared ordering flows through again.
   */
  resetSortFromBar(): void {
    this.applySort(this.resolution.defaultMultiSortMeta() ?? []);
  }

  /**
   * Puts the entering view's sort on the grid — stored override, else declared sort, else the
   * table default, else UNSORTED. A view is a COMPLETE state, sort included (2026-09-06,
   * grilled — supersedes the "a view stating no preference keeps the ordering already there"
   * this first shipped with): the carried-over ordering read as a bug in its first live week,
   * when "Poslate"'s statusChangedAt rode onto "Sve", whose sorted column ships hidden —
   * read-only chip, no header to tri-state, no way out. Polaris IndexFilters is the model:
   * sort is part of a view's saved definition. Broadcast only — the caller owns the refetch,
   * because which request carries the new sort differs between the store and storeless paths.
   */
  applyViewSort(): boolean {
    const target = this.resolution.resolvedViewSort() ?? [];
    if (sortMetaEquals(target, this.table._multiSortMeta ?? [])) return false;

    this.broadcastSort(target);
    return true;
  }

  // Safety net: any lazy load still leaving unsorted (stale persisted state; a viewless
  // table with no sort memory yet) falls back to the declared default. Patching the event
  // before it becomes the backend Filter avoids a second fetch; the re-broadcast keeps the
  // header icons agreeing with what was actually sent. The tri-state un-sort path never
  // gets here — it substitutes the default at its source in setupRemovableSort().
  applyDefaultSortIfUnsorted(event: TableLazyLoadEvent): void {
    if (event.multiSortMeta?.length) return;

    const defaultSort = this.resolution.defaultMultiSortMeta();
    if (defaultSort == null) return;

    event.multiSortMeta = defaultSort;

    if (this.table) this.broadcastSort(defaultSort);
  }

  /** The grid's sort, as the active view's override: each sort gesture records it. */
  private recordOverride(): void {
    this.resolution.persistActiveViewSort(this.table._multiSortMeta ?? []);
  }

  /** Puts `meta` on the grid and tells the header icons — no fetch; the caller owns that. */
  private broadcastSort(meta: SortMeta[]): void {
    this.table._multiSortMeta = meta;
    this.table.tableService.onSort(meta);
  }

  /**
   * The sort half of the legacy "hidden contributes nothing" invariant, kept ONLY for storeless
   * tables (the generated M2M details grids, until spiderly#407 scaffolds their stores): they
   * have no sort chip, so a hidden column's sort would order the grid with nothing on screen
   * naming it and no control left to clear it. The filter half needs no heir — `lazyLoad`
   * overwrites the outgoing filters unconditionally. On a store table this is a no-op:
   * decision 2b's chips are the visible surface, hidden column or not.
   */
  dropHiddenColumnSort(cols: Column[]): void {
    if (!this.table || this.host.filters) return;

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
   * Read off the last REQUEST's sort, not `table._multiSortMeta`. On a first load with a declared
   * default that field is still null: `applyDefaultSortIfUnsorted` writes the default onto the
   * lazy-load event, and PrimeNG fills its own meta only once someone clicks a header. Reading the
   * table there showed no sort on exactly the tables that always have one. A field with no column
   * left falls back to its own name rather than vanishing.
   */
  refreshSortKeys(requestSort: SortMeta[] | null | undefined): void {
    const meta = requestSort ?? [];

    this.sortKeys = meta.map((key) => ({
      field: key.field,
      // A sort may name a field no column declares (keepSortableMeta allows it), and its chip
      // must still speak the operator's language: the same PascalCase-key convention the
      // scaffold seeds resolves it (Transloco falls back to the key, so worst case reads
      // "CreatedAt", never the raw camelCase identifier).
      label:
        this.host.cols.find((col) => col.field === key.field)?.name ??
        this.translocoService.translate(firstCharToUpper(key.field)),
      descending: key.order === -1,
    }));
    this.sortIsDefault = sortMetaEquals(
      meta,
      this.resolution.defaultMultiSortMeta() ?? [],
    );
  }
}
