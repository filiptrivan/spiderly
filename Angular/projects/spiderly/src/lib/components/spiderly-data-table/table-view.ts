// A saved question of a `spiderly-data-table`, as a consumer declares it.
import { SortMeta } from 'primeng/api';

/**
 * A saved question. The bar lets an operator BUILD one; a view is the one they ask every morning,
 * asked in a click — and without them the whole column-and-filter rework is a pile of knobs
 * someone re-sets daily (decision 10).
 *
 * Declared in consumer code, not stored per user: with a handful of operators and a handful of
 * fixed daily jobs, shared built-in views cover the work, while personal saved views would drift
 * into five variants of the same question. Personal LAYOUT choices still persist on top, per view.
 */
export interface TableView<TFilters = any, TRow = any> {
  id: string;
  label: string;
  /**
   * Sets the filters this view asks for. Receives the table's own store, so a consumer writes it
   * with the store's typing. Called on a CLEARED store: a view is a state, not an addition.
   *
   * `viewFilterPayload` also calls it, on a scratch store of the same declarations, to read what
   * entering the view would send, so it writes into the store it receives and nothing else.
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
   * declaring none falls back to the table's `defaultSortField`, else unsorted — it never keeps
   * the previous view's ordering (`applyViewSort`).
   */
  sort?: SortMeta[];
  /**
   * How many rows the view holds, shown as a badge beside its label so an operator sees which
   * work list has something in it without clicking through every tab (Polaris IndexFilters
   * carries the same badge). `null` or absent shows no badge; `0` shows one.
   *
   * The table never computes it: the consumer runs its own count query and hands the number in,
   * typically by rebuilding `[views]` after every list load. Such a rebuild is safe — it neither
   * re-enters the active view nor sends a request (see `storedViewToAdopt`). For a count that
   * honors the bar filters an operator saved on each view, send each view's
   * `viewFilterPayload(id)` with that query.
   */
  count?: number | null;
  /**
   * The view's own column defaults, as field → visible: `{ why: true, location: false }` shows
   * `why` and hides `location` in this view only, overriding their `Column.visible`. A column the
   * map does not name keeps its `Column.visible`, so a column added to the table later shows up
   * in every view that does not hide it. Place a column a view swaps in next to the one it
   * replaces in `cols`, and it takes that column's place.
   *
   * The operator's own per-view choices still win, and are stored only where they differ from
   * this declaration, so a changed declaration reaches every operator who never touched that
   * column; Reset returns to it. A `lockVisible` column always shows, whatever the map says.
   * Rebuilding `[views]` with a changed map re-renders the active view's columns.
   */
  columns?: Partial<Record<string & keyof TRow, boolean>>;
}
