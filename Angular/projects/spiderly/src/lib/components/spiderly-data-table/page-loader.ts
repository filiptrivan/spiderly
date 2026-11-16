import { effect, ErrorHandler, inject, Injector } from '@angular/core';
import { TableLazyLoadEvent } from 'primeng/table';
import { Filter } from '../../entities/filter';
import {
  exportListToExcel,
  scrollElementIntoViewIfAboveViewport,
} from '../../services/helper-functions';
import { ActiveView } from './active-view';
import { RowSelection } from './row-selection';
import type { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { TableSort } from './table-sort';
import { TableStateStorage } from './table-state-storage';

/**
 * The table's inputs that the loader reads. The loader also writes the row state that the
 * template binds: `items`, `totalRecords`, `lastLazyLoadEvent` and `rowsPerPageOptions`.
 */
export type PageLoaderHost = Pick<
  SpiderlyDataTableComponent,
  | 'table'
  | 'items'
  | 'totalRecords'
  | 'lastLazyLoadEvent'
  | 'rows'
  | 'rowsPerPageOptions'
  | 'filters'
  | 'additionalFilterIdLong'
  | 'additionalIndexes'
  | 'getPaginatedListObservableMethod'
  | 'exportListToExcelObservableMethod'
  | 'getFormArrayItems'
  | 'onLazyLoad'
  | 'onTotalRecordsChange'
>;

/**
 * Loads a `spiderly-data-table`'s rows: a page from the server for a lazy table, the form array
 * for a client-side one. The pending flag is true for exactly as long as a fetch is in flight.
 * The loader re-queries when the applied filters change, and it owns the paginator's page sizes.
 * Construct it in the table's injection context, for `ErrorHandler` and the effect's injector.
 */
export class PageLoader {
  private readonly errorHandler = inject(ErrorHandler);
  private readonly injector = inject(Injector);

  private loading: boolean = true;

  constructor(
    private readonly host: PageLoaderHost,
    private readonly storage: TableStateStorage,
    private readonly activeView: ActiveView,
    private readonly sort: TableSort,
    private readonly selection: RowSelection,
  ) {}

  /**
   * The one pending predicate: PrimeNG's overlay and the container's `aria-busy` must never
   * disagree about whether a fetch is in flight.
   *
   * It used to also test `items === undefined`, for the first load. That clause is dead now
   * that `loading` initialises true and every lazyLoad raises it — except in the one state
   * where it was actively wrong: a FAILED first load lowers the flag with `items` still
   * undefined, which pinned the overlay up forever. Same stranded overlay this component was
   * fixed for once already, through the other door.
   *
   * It does not give screen-reader parity. `aria-busy` defers announcements and makes none: a
   * screen reader hears nothing when a page flip starts or ends. A live region is a library-wide
   * decision (an `sr-only` utility and a key in the library's `i18n/en.json`). There is
   * deliberately no opt-out input.
   */
  get isPending(): boolean {
    return this.loading;
  }

  load(event: TableLazyLoadEvent) {
    // Every refetch is pending, not just the first load and reload(). `items` is deliberately
    // left alone: the previous page stays on screen under the overlay (stale-while-revalidate)
    // rather than blanking. Raising the flag is also what stops PrimeNG's empty message, gated
    // on `isEmpty() && !loading`, from claiming "no records" for the length of the request.
    this.loading = true;

    this.sort.applyDefaultSortIfUnsorted(event);
    this.host.lastLazyLoadEvent = event;
    this.sort.refreshSortKeys(event.multiSortMeta);
    this.selection.forgetAnchor();

    let tableFilter: Filter = event as unknown as Filter;
    tableFilter.additionalFilterIdLong = this.host.additionalFilterIdLong;

    // The store is the ONLY source of what narrows the grid, on every table. Unconditional on
    // purpose: with the header filters gone, the one remaining writer of PrimeNG's own
    // `event.filters` is a stale persisted blob from the header-filter era replayed by
    // `restoreState` — and letting that through would narrow a storeless table invisibly, with
    // no chip and no button left to clear it.
    tableFilter.filters = this.host.filters?.toFilterPayload() ?? {};

    this.host.onLazyLoad.next(tableFilter);

    // Issued NOW, beside the list request: `requestSelectedIds` says why.
    const selectedIds = this.selection.requestSelectedIds(tableFilter);

    this.host.getPaginatedListObservableMethod(tableFilter).subscribe({
      next: async (res) => {
        // The await below is INSIDE an async subscriber, so a rejection there settles nothing:
        // `error:` belongs to the paginated-list observable and never runs. That used to strand
        // the overlay forever, hence the finally.
        try {
          this.host.items = res.data;
          this.host.totalRecords = res.totalRecords;
          this.host.onTotalRecordsChange.next(res.totalRecords);

          await this.selection.reconcileLoadedPage(selectedIds);
        } catch (error) {
          // The library's own owner: it toasts, it skips HttpErrorResponse (the interceptor
          // already reported those), and a consumer's ErrorHandler wrapper forwards to their
          // tracker. console.error here would have dropped both.
          this.errorHandler.handleError(error);
        } finally {
          // After the selection await, deliberately. Lowered earlier, the new page paints with
          // the previous page's checked rows, since `fakeSelectedItems` still holds their ids.
          this.loading = false;
        }
      },
      error: () => {
        this.loading = false;
      },
    });
  }

  loadClient() {
    this.loading = false;

    this.loadFormArrayItems();
    this.host.totalRecords = this.host.items.length;
    this.host.onTotalRecordsChange.next(this.host.items.length);

    this.selection.loadClientSelection();
  }

  private loadFormArrayItems() {
    this.host.items = this.host.getFormArrayItems(this.host.additionalIndexes);
    this.selection.forgetAnchor();
    this.host.items.forEach((item, index) => {
      item.index = index;
    });
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
  requeryOnAppliedFilters(): void {
    let isFirstRun = true;

    effect(
      () => {
        this.host.filters!.applied();

        if (isFirstRun) {
          isFirstRun = false;
          return;
        }

        this.activeView.persistAppliedFilters();
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
  refetchFromFirstPage(): void {
    this.host.table.first = 0;
    this.host.table.firstChange.emit(0);
    if (this.host.table.isStateful()) this.host.table.saveState();
    this.load(this.host.table.createLazyLoadMetadata());
  }

  reload() {
    // Nothing to replay before the table's own initial load, which is already on its way.
    if (this.host.lastLazyLoadEvent == null) return;

    // Not special: same refetch as a page flip, so `load` owns both the flag and `items`.
    this.load(this.host.lastLazyLoadEvent);
  }

  /** Sends the last page request's filter to the export endpoint. */
  exportListToExcel() {
    let filter: Filter = this.host.lastLazyLoadEvent as unknown as Filter;
    filter.additionalFilterIdLong = this.host.additionalFilterIdLong;

    exportListToExcel(this.host.exportListToExcelObservableMethod, filter);
  }

  /**
   * A page size PrimeNG will actually apply but that is missing from `rowsPerPageOptions` leaves
   * the paginator dropdown blank — so both the effective `rows` and the persisted pick (which
   * `restoreState` applies after init) are merged in. Runs again on input changes because a
   * consumer may resolve `[rows]` asynchronously. Storage is user-writable, hence the shape and
   * ceiling check: a hand-edited `{"rows": 100000}` must not become an offered choice, since the
   * backend `.Take`s whatever it is sent.
   */
  mergeActivePageSizesIntoOptions(): void {
    const ceiling = Math.max(...this.host.rowsPerPageOptions);
    const persisted = this.storage.readTableState()?.rows;
    const candidates = [
      this.host.rows,
      Number.isInteger(persisted) && persisted > 0 && persisted <= ceiling
        ? persisted
        : null,
    ];

    for (const value of candidates) {
      if (value != null && !this.host.rowsPerPageOptions.includes(value)) {
        this.host.rowsPerPageOptions = [
          ...this.host.rowsPerPageOptions,
          value,
        ].sort((a, b) => a - b);
      }
    }
  }

  /**
   * Puts the table's wrapper back under the top of the viewport after a page change: paging
   * leaves the reader at the paginator, with the new rows above the screen. It is unconditional,
   * with no opt-out input. PrimeNG holds the same policy for `[scrollable]` tables, whose page
   * change resets the container's scroll. That branch never runs here: this table uses
   * `responsiveLayout="scroll"`, so the window scrolls, not a container.
   *
   * Why the hook is `(onPage)` and the target is the wrapper: the comment above `<p-table>` in
   * the template. The guard and the offset: `scrollElementIntoViewIfAboveViewport()`.
   */
  onPageChange(container: HTMLElement): void {
    scrollElementIntoViewIfAboveViewport(container);
  }
}
