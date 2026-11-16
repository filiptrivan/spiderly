import { ColumnLayout } from './column-layout';
import { Column } from './data-table-column';

/**
 * Header drag, ours rather than PrimeNG's `pReorderableColumn`, for two reasons: its
 * `onMouseDown` arms the th for anything that is not an INPUT, a TEXTAREA or its OWN resizer —
 * so our menu chevron and our resize grip would each start a column drag, and reaching for a
 * width would silently reorder the grid — and its `onColumnDrop` reorders blind, with no notion
 * of a locked column (decision 6).
 *
 * A shortcut for neighbours, never the mechanism: HTML5 dnd has no edge auto-scroll, so a column
 * scrolled off the right is unreachable this way, and there is no keyboard path at all. The menu
 * carries both.
 *
 * `onHeaderMouseDown` is the arming rule; the drop rule is `ColumnLayout.canDropOn`.
 */
export class HeaderDrag {
  private draggedColumn: Column | null = null;

  /** False while a gesture that began on the menu chevron or the resize grip is in flight. */
  private headerDragArmed = true;

  constructor(private readonly layout: ColumnLayout) {}

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
    if (this.layout.canDropOn(col)) event.preventDefault();
  }

  onHeaderDrop(col: Column, event: DragEvent): void {
    event.preventDefault();

    const dragged = this.draggedColumn;
    this.draggedColumn = null;
    if (!dragged || dragged === col || !this.layout.canDropOn(col)) return;

    this.layout.moveColumnOnto(dragged, col);
  }
}
