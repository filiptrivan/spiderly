import { ActiveView } from './active-view';
import { ColumnLayout } from './column-layout';
import { PageLoader } from './page-loader';
import type { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { TableSort } from './table-sort';
import { TableView } from './table-view';

/** The table's grid and store, which entering a view sorts and filters. */
export type ViewSwitchHost = Pick<SpiderlyDataTableComponent, 'table' | 'filters'>;

/**
 * Entering a `spiderly-data-table` view: its layout, its sort and its filters go on the grid, and
 * one request carries them. A click enters a view, and so does a late `[views]` that brings the
 * stored one.
 */
export class ViewSwitch {
  constructor(
    private readonly host: ViewSwitchHost,
    private readonly activeView: ActiveView,
    private readonly layout: ColumnLayout,
    private readonly sort: TableSort,
    private readonly pageLoader: PageLoader,
  ) {}

  /**
   * Switches to a view. Clears first, always: a view is a STATE rather than an addition, and two
   * of them composed produce a question nobody asked. The clear runs through the store directly
   * rather than through `clear()`, whose requery effect would spend a fetch before the entering
   * view's filters are in place.
   */
  selectView(view: TableView): void {
    this.activeView.select(view);

    // The keys just changed under us, so the layout has to be re-read for the view being entered
    // — otherwise the one being left keeps rendering until something else forces a reload.
    this.layout.restore();

    // Sort is view state too (decision 10's sort half). Swapped BEFORE the filter transition so
    // the one request the requery effect sends carries both.
    const sortChanged = this.sort.applyViewSort();

    const filters = this.host.filters;
    if (!filters) {
      if (sortChanged) this.host.table.sortMultiple();
      return;
    }

    const appliedBefore = filters.applied();

    // The filter key moved with the view too; `enterViewFilters` holds the stored-wins and
    // transient rules.
    filters.clear();
    this.activeView.enterViewFilters(view, 'click');

    // The requery effect runs only when the applied set actually changed — `clear()` on an
    // already-empty store writes nothing (two views sharing one filter answer are exactly that)
    // — so a sort-only switch fetches here. `applied` is a computed: reference equality across
    // the transition means none of its sources moved.
    if (sortChanged && filters.applied() === appliedBefore) {
      this.pageLoader.refetchFromFirstPage();
    }
  }

  /** Enters the stored view that a `[views]` change brought: `ActiveView.storedViewToAdopt`. */
  adoptStoredView(previousViews: TableView[] | undefined): void {
    const view = this.activeView.storedViewToAdopt(previousViews);
    if (view) this.selectView(view);
  }
}
