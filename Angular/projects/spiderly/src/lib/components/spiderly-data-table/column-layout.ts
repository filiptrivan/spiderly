import { columnShare, columnWidth } from './column-shares';
import { Column } from './data-table-column';
import { TableStateStorage } from './table-state-storage';
import { TableView } from './table-view';

/** A data column shows values for a `field`; anything else (the actions column) always renders. */
export function isDataColumn(col: Column): boolean {
  return !!col.field;
}

/** The view whose layout is on screen. Its id scopes the stored layout, its `columns` the defaults. */
export interface LayoutScope {
  readonly id: string | null;
  view(): TableView | undefined;
}

/**
 * The operator's layout of a `spiderly-data-table`'s columns: which show, in what order, which
 * wrap, and how wide. Each view keeps its own layout, in localStorage. `onHide` hears of the
 * columns that a toggle or a reset hides.
 */
export class ColumnLayout {
  /** Columns currently rendered — `cols` minus the hidden ones. Actions columns always render. */
  visibleCols: Column[] = [];

  /** Data columns offered in the column chooser (actions columns are excluded). */
  chooserCols: Column[] = [];

  /** Cached count of visible data columns, so template bindings don't re-derive it per CD cycle. */
  private visibleDataColsCount = 0;

  /**
   * User visibility choices (field → visible), holding only fields the user explicitly
   * toggled away from their declared default — so a page changing a column's declared
   * default later flows through to every user who never touched that column.
   */
  private columnVisibilityOverrides: Record<string, boolean> = {};

  /**
   * Per-column wrap, by `field`. A SEPARATE key from the visibility overrides on purpose: folding
   * both into one object would invalidate every layout already in an operator's localStorage, and
   * this is the key the width and order overrides will join (decision 5).
   */
  private columnWrap: Record<string, boolean> = {};

  /** Data-column fields in the operator's order. Empty means "as declared". */
  private columnOrder: string[] = [];

  /**
   * Per-column width overrides, as SHARES in the same unit `columnWidth` emits — never pixels.
   * Under the fixed layout the browser splits surplus in proportion to these, so a pixel would
   * pin the column and stop it answering the window: the operator on a 1280 laptop and the one
   * on a 2560 monitor would get the same frozen column (decision 4).
   */
  private columnWidths: Record<string, number> = {};

  constructor(
    private readonly host: { readonly cols: Column[] },
    private readonly storage: TableStateStorage,
    private readonly scope: LayoutScope,
    private readonly onHide: (hidden: Column[]) => void,
  ) {}

  /** The first restore, at init. It also fixes the chooser's list: columns are a declaration. */
  init(): void {
    this.chooserCols = this.host.cols.filter(isDataColumn);
    this.restore();
  }

  /** Re-reads the scope's stored layout, as entering a view must, and renders it. */
  restore(): void {
    if (this.storage.persists) {
      this.columnVisibilityOverrides =
        this.storage.read('columns', this.scope.id) ?? {};

      const layout = this.storage.read('layout', this.scope.id);
      this.columnWrap = layout?.wrap ?? {};
      this.columnOrder = layout?.order ?? [];
      this.columnWidths = layout?.widths ?? {};
    }

    this.refresh();
  }

  /** Every template loop iterates `visibleCols`, so a change to visibility or order calls this. */
  refresh(): void {
    this.visibleCols = this.orderedCols().filter((col) =>
      this.isColumnVisible(col),
    );
    this.visibleDataColsCount = this.visibleCols.filter(isDataColumn).length;
  }

  colTrackByFn(index, item) {
    return item.field;
  }

  /** Actions columns always render; data columns follow the override, then declared default. */
  isColumnVisible(col: Column): boolean {
    if (!isDataColumn(col)) return true;
    if (col.lockVisible) return true; // pinned — wins over any (possibly stale) override
    return this.columnVisibilityOverrides[col.field] ?? this.isDeclaredVisible(col);
  }

  /** The declared default in the current context: the active view's `columns`, then `Column.visible`. */
  private isDeclaredVisible(col: Column): boolean {
    return this.scope.view()?.columns?.[col.field] ?? col.visible !== false;
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
    if (visible === this.isDeclaredVisible(col)) {
      delete this.columnVisibilityOverrides[col.field]; // back at the declared default
    } else {
      this.columnVisibilityOverrides[col.field] = visible;
    }

    this.persistColumnVisibility();
    this.refresh();

    if (!visible) this.onHide([col]);
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

    const wasVisible = this.visibleCols.filter(isDataColumn);

    this.columnVisibilityOverrides = {};
    this.persistColumnVisibility();
    this.refresh();

    // Columns the reset just hid follow the same rule as a manual hide.
    this.onHide(wasVisible.filter((col) => !this.isColumnVisible(col)));
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

  /** The menu's move: one place in `direction`, when `canMoveColumn` allows it. */
  moveColumn(col: Column, direction: -1 | 1): void {
    if (!this.canMoveColumn(col, direction)) return;

    const order = this.dataColumnsInOrder();
    this.placeColumn(order, col, order.indexOf(col) + direction);
  }

  /** The drag's move: `dragged` takes the place of `target`, which `canDropOn` allowed. */
  moveColumnOnto(dragged: Column, target: Column): void {
    const order = this.dataColumnsInOrder();
    this.placeColumn(order, dragged, order.indexOf(target));
  }

  /** Nothing lands on, or in front of, the locked column. Enforced here as well as in the menu:
   * a drop is a separate entry point, and the rule is about the grid rather than about a button. */
  canDropOn(col: Column): boolean {
    return !col.lockVisible && isDataColumn(col);
  }

  /**
   * Whether this column's cells give up the one-line clamp. The DEFAULT stays clamped
   * (decision 9); what changes is that the operator picks which column pays the row height, not
   * whoever wrote `cols`.
   *
   * Lands on the td as `.cell-wrap`, whose inherited `--spiderly-cell-*` properties are how the
   * toggle crosses into a projected `spiderlyCellTemplate` too — the contract's one telling is
   * on that SCSS rule; a consumer clamp that hard-codes its values opts out.
   */
  isColumnWrapped(col: Column): boolean {
    return col.field != null && this.columnWrap[col.field] === true;
  }

  toggleWrap(col: Column): void {
    if (this.columnWrap[col.field]) delete this.columnWrap[col.field];
    else this.columnWrap[col.field] = true;

    this.persistColumnLayout();
  }

  /** This column's share, override first, then its declared width, then the per-type default. */
  columnShare(col: Column): number {
    return columnShare(col, this.widthOverride(col));
  }

  /** The width the column's header renders at: `columnWidth`, with the operator's override. */
  getColWidth(col: Column): string {
    return columnWidth(col, this.widthOverride(col));
  }

  /** Stores the shares a resize or a fit gives, by `field`. */
  setShares(shares: Record<string, number>): void {
    Object.assign(this.columnWidths, shares);
    this.persistColumnLayout();
  }

  private widthOverride(col: Column): number | undefined {
    return col.field ? this.columnWidths[col.field] : undefined;
  }

  /**
   * `cols` in the operator's order. A field the stored order has never seen — a column added in a
   * later release — sorts to the END rather than displacing a layout someone built by hand
   * (decision 7); the sort is stable, so those keep their declared order among themselves.
   * Actions columns carry no field and are always last, which is where every consumer already
   * declares them.
   */
  private orderedCols(): Column[] {
    if (this.columnOrder.length === 0) return this.host.cols;

    const rank = new Map(
      this.columnOrder.map((field, index) => [field, index]),
    );

    return [...this.host.cols].sort(
      (a, b) =>
        (rank.get(a.field!) ?? Number.MAX_SAFE_INTEGER) -
        (rank.get(b.field!) ?? Number.MAX_SAFE_INTEGER),
    );
  }

  /** The move itself, shared by the menu and the drag — two entry points, one rule. */
  private placeColumn(order: Column[], col: Column, to: number): void {
    order.splice(to, 0, ...order.splice(order.indexOf(col), 1));

    this.columnOrder = order.map((entry) => entry.field!);
    this.persistColumnLayout();
    this.refresh();
  }

  private dataColumnsInOrder(): Column[] {
    return this.orderedCols().filter(isDataColumn);
  }

  /** One key for the whole layout — wrap now, widths next (decision 5). */
  private persistColumnLayout(): void {
    const isDefault =
      Object.keys(this.columnWrap).length === 0 &&
      this.columnOrder.length === 0 &&
      Object.keys(this.columnWidths).length === 0;

    this.storage.write(
      'layout',
      this.scope.id,
      isDefault
        ? null
        : {
            wrap: this.columnWrap,
            order: this.columnOrder,
            widths: this.columnWidths,
          },
    );
  }

  private persistColumnVisibility(): void {
    this.storage.write(
      'columns',
      this.scope.id,
      Object.keys(this.columnVisibilityOverrides).length === 0
        ? null
        : this.columnVisibilityOverrides,
    );
  }
}
