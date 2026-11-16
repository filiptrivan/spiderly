import { Popover } from 'primeng/popover';
import { FilterSource } from '../../filters/filter-store';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { ColumnLayout } from './column-layout';
import { ColumnResize } from './column-resize';
import { Column } from './data-table-column';
import { isColumnSortable } from './sort-resolution';
import { TableSort } from './table-sort';


/**
 * The menu a `spiderly-data-table` header opens: hide, filter, sort, move, fit and wrap its column.
 * Each item closes the menu, then asks the module that owns the change.
 */
export class HeaderMenu {
  /**
   * The column the header menu is currently open for, and its header cell. One menu serves every
   * column: its items render once and read their target here. PrimeNG does not re-project popover
   * content that an `@if` destroyed, so a menu rebuilt per column reopens as an empty overlay.
   */
  column: Column | null = null;

  private headerCell: HTMLElement | null = null;

  constructor(
    private readonly host: { readonly filters?: FilterSource },
    private readonly popover: () => Popover,
    private readonly filterBar: () => SpiderlyFilterBarComponent | undefined,
    private readonly layout: ColumnLayout,
    private readonly sort: TableSort,
    private readonly resize: ColumnResize,
  ) {}

  // Whether each item is enabled for the column the menu is open for.
  get canHide(): boolean {
    return this.column != null && this.layout.canToggleColumn(this.column);
  }

  get canFilter(): boolean {
    return this.column != null && this.canFilterColumn(this.column);
  }

  get canSort(): boolean {
    return this.column != null && isColumnSortable(this.column);
  }

  canMove(direction: -1 | 1): boolean {
    return this.column != null && this.layout.canMoveColumn(this.column, direction);
  }

  get canWrap(): boolean {
    return this.column != null && this.column.editable !== true;
  }

  open(col: Column, event: Event): void {
    this.column = col;
    // Kept so `fitColumn` can find the column's cells: it needs the header's position among
    // its siblings, which is the only thing that ties a Column to a table column in the DOM.
    this.headerCell = (event.target as HTMLElement).closest('th');
    this.popover().toggle(event);
  }

  hideColumn(): void {
    if (this.column) this.layout.toggleColumn(this.column, false);
    this.popover().hide();
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
  private canFilterColumn(col: Column): boolean {
    const id = this.filterIdFor(col);

    return id != null && this.host.filters?.definitions[id] != null;
  }

  /**
   * Opens the bar's editor for this column's filter. The bar took the filter off the header and
   * the header is still where an operator reaches for it, which is twenty-seven tables' worth of
   * habit; this is the shortcut back rather than a second filter surface — it drives the same
   * editor, on the same bar, that `+ Filter` opens.
   */
  filterColumn(): void {
    const col = this.column;
    this.popover().hide();
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
  sortColumn(order: 1 | -1): void {
    const col = this.column;
    this.popover().hide();
    if (!col?.field || !isColumnSortable(col)) return;

    this.sort.applySort([{ field: col.field, order }]);
  }

  /** Sizes the column to the widest thing in it: `ColumnResize.fit`. */
  fitColumn(): void {
    const col = this.column;
    const th = this.headerCell;
    this.popover().hide();
    if (!col?.field || !th?.parentElement) return;

    this.resize.fit(col, th);
  }

  moveColumn(direction: -1 | 1): void {
    const col = this.column;
    this.popover().hide();
    if (col) this.layout.moveColumn(col, direction);
  }

  get isColumnWrapped(): boolean {
    return this.column != null && this.layout.isColumnWrapped(this.column);
  }

  toggleWrap(): void {
    const col = this.column;
    this.popover().hide();
    if (!col?.field) return;

    this.layout.toggleWrap(col);
  }
}
