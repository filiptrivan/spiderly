import { untracked } from '@angular/core';
import { FilterRule } from '../../entities/filter-rule';
import { createFilterStore, FilterSource } from '../../filters/filter-store';
import { LayoutScope } from './column-layout';
import { TableStateStorage } from './table-state-storage';
import { TableView } from './table-view';

/** The table's inputs that declare its views and hold its filters. */
export interface ActiveViewHost {
  readonly views?: TableView[];
  readonly filters?: FilterSource;
}

/**
 * Which of a `spiderly-data-table`'s views is active, how a reload and a late `[views]` find it
 * again, and the filters that entering a view applies. Its id scopes the layout and sort keys.
 */
export class ActiveView implements LayoutScope {
  /**
   * The view currently selected. Seeded from the first view in `ngOnInit` — then overridden by
   * `restoreActiveView`, so a reload lands where the operator was — and never left null on a
   * table that has views: the storage keys the layout off it, so an unseeded table writes its
   * width and order to the SAME key an unmigrated table uses — orphaned the moment the operator
   * clicks the first tab — and no tab reads as selected until they do.
   */
  id: string | null = null;

  constructor(
    private readonly host: ActiveViewHost,
    private readonly storage: TableStateStorage,
  ) {}

  view(): TableView | undefined {
    return this.id ? this.viewById(this.id) : undefined;
  }

  viewById(id: string): TableView | undefined {
    return this.host.views?.find((candidate) => candidate.id === id);
  }

  /** By id: a count refresh rebuilds `[views]` as new objects, and must not re-create the focused tab. */
  viewTrackByFn(index: number, view: TableView) {
    return view.id;
  }

  /**
   * Makes `view` the active view and stores its id. Always writes — including the first view, so
   * a deliberate "Sve" during a deploy race sticks, and so an explicit pick beats a stored id
   * whose view has not arrived yet (`storedViewToAdopt` compares against this key). The
   * `ngOnInit` seed does not write: absence means "the default".
   */
  select(view: TableView): void {
    this.id = view.id;
    if (this.id) this.storage.writeActiveViewId(this.id);
  }

  /**
   * Puts a reload back on the view the operator was using. Every persisted key carries the view
   * segment, so before this existed the seed decided what got restored: F5 landed on the first
   * view and read `…:all:filters` — the tab snapped to "Sve" and the bar came back empty while
   * the operator's filters sat intact under the old view's segment (Filip, on /porudzbine).
   *
   * An id naming no current view keeps the seed AND the stored key: "vanished by deploy" and
   * "not yet arrived" are indistinguishable here (order-list's catalog-licensed "Za pakovanje"
   * is literally both), so `storedViewToAdopt` re-reads the key on every `[views]` change and the
   * key self-heals on the next explicit click.
   */
  restoreActiveView(): TableView | null {
    if (!this.storage.persists || !this.host.views?.length) return null;

    const storedId = this.storage.readActiveViewId();
    if (typeof storedId !== 'string') return null;

    const view = this.viewById(storedId) ?? null;
    if (view) this.id = view.id;

    return view;
  }

  /**
   * The second half of `restoreActiveView`: the view to adopt, when a later `[views]` change
   * brings a stored view that did not exist at init. The table adopts it through `selectView`,
   * so it gets the same
   * stored-wins/transient/layout/requery semantics a click would have had. There is no parked
   * copy of the stored id — the key itself is re-read, and because every explicit pick
   * overwrites it, the operator's own choice wins by construction. Accepted cost of adopting
   * late instead of holding the first request: a brief flash of the seeded view's rows and a
   * second request when the late view lands.
   *
   * Only a view this change BROUGHT is adopted. A consumer rebuilds `[views]` on every list load
   * to refresh `TableView.count`, and under `stateStorage: 'local'` the key can name a view the
   * operator picked in another browser tab — adopting on every change switched this tab's view
   * at the next count refresh (spiderly#430).
   */
  storedViewToAdopt(previousViews: TableView[] | undefined): TableView | undefined {
    if (!this.storage.persists) return undefined;

    const storedId = this.storage.readActiveViewId();
    if (typeof storedId !== 'string' || storedId === this.id) return undefined;
    if (previousViews?.some((view) => view.id === storedId)) return undefined;

    return this.viewById(storedId);
  }

  persistAppliedFilters(): void {
    if (!this.host.filters) return;

    const snapshot = this.host.filters.snapshot();

    this.storage.write(
      'filters',
      this.id,
      Object.keys(snapshot).length === 0 ? null : snapshot,
    );
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
   *
   * `viewFilterPayload` passes a scratch store and another view's id, to read what entering
   * that view would send.
   */
  enterViewFilters(
    view: TableView | null,
    semantics: 'click' | 'refresh',
    store: FilterSource | undefined = this.host.filters,
    viewId: string | null = this.id,
  ): void {
    if (!store) return;

    const stored = !view?.transient ? this.storage.read('filters', viewId) : null;

    if (stored) store.restore(stored);
    else if (view?.transient || semantics === 'click') view?.apply?.(store);
  }

  /**
   * The `filters` payload a view's own list request carries — the same shape the store's
   * `toFilterPayload()` emits — so a consumer's count query can narrow each view by the bar
   * filters the operator saved on it, and a `TableView.count` stays the number of rows that tab
   * lists: on screen for the active view, after a click for the others (spiderly#439).
   *
   * - The ACTIVE view answers with the live filters. Not with what a click would enter: clicking
   *   the active tab re-asks its question when the operator has cleared the bar, but the list on
   *   screen, and so its count, has not.
   * - Any other view answers with what a click would enter (`enterViewFilters`): the filters
   *   saved on it, else its `apply` — always `apply` for a transient view. That runs on a scratch
   *   `createFilterStore` of the same declarations, so nothing switches, persists or re-queries.
   *   Untracked: the scratch store's signal writes would otherwise make a read inside a
   *   `computed` throw NG0600.
   *
   * `{}` on a table without `[filters]`, whose requests carry none; `null` for an id that is not
   * in `[views]`.
   */
  viewFilterPayload(viewId: string): Record<string, FilterRule[]> | null {
    const view = this.viewById(viewId);
    if (!view) return null;
    if (!this.host.filters) return {};
    if (view.id === this.id) return this.host.filters.toFilterPayload();

    const definitions = this.host.filters.definitions;
    return untracked(() => {
      const scratch = createFilterStore(definitions);
      this.enterViewFilters(view, 'click', scratch, view.id);

      return scratch.toFilterPayload();
    });
  }
}
