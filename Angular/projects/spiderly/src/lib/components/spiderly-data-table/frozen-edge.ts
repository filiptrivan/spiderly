import { inject, NgZone } from '@angular/core';
import { ColumnLayout } from './column-layout';
import { Column } from './data-table-column';

/**
 * The pinned left edge of a `spiderly-data-table`: the checkbox column, then the identity column,
 * stay in view while the grid scrolls sideways. Construct it in the table's injection context, for
 * `NgZone`.
 */
export class FrozenEdge {
  private readonly zone = inject(NgZone);

  /**
   * The selection column's declared share. Under the fixed layout this is a RATIO, not a width.
   *
   * 4rem, down from 6: the original reserved room for a five-digit select-all counter sitting at
   * body text size beside the box, which is the widest thing it can ever hold and not what it
   * holds on any ordinary screen. The counter is a smaller step now (see the SCSS), so the same
   * five digits fit in two thirds of the space.
   */
  readonly selectionColumnWidth = '4rem';

  /**
   * The selection column's MEASURED width, which is what the frozen identity column offsets
   * itself by. Declaring `6rem` on both looked like the one-telling answer and is wrong: widths
   * are shares, so that column renders at whatever proportion of the table it gets — 148px for a
   * declared 6rem in one spec — and the identity column would sit 52px inside it, overlapping the
   * checkboxes the moment anyone scrolled. The spec caught it; nothing about the page would have.
   */
  frozenOffsetPx = 0;

  private selectionWidthObserver?: ResizeObserver;

  constructor(
    private readonly host: {
      readonly selectionMode: 'single' | 'multiple' | undefined | null;
    },
    private readonly layout: ColumnLayout,
  ) {}

  /**
   * Whether this column is pinned against the left edge. Only the FIRST column, and only when it
   * is `lockVisible`: a sticky column in the middle of the grid pins the wrong thing and leaves a
   * hole where it used to be. Once the grid scrolls sideways a row otherwise loses its identity
   * — "Intesa, plaćeno, 12.400" with no idea whose order it is — and NN/g's Data Tables is
   * explicit that the leftmost header column must lock in place. `lockVisible` already names
   * exactly that column in every table, so this needs no new API (decision 8).
   */
  isColumnFrozen(col: Column): boolean {
    return col.lockVisible === true && this.layout.visibleCols[0] === col;
  }

  /**
   * The rightmost pinned cell, which carries the seam's shadow. It is the identity column when
   * there is one and the checkbox column otherwise — a fact only this side knows, which is why
   * the SCSS asking for it as `:last-of-type` drew nothing at all.
   */
  isLastFrozenColumn(col: Column): boolean {
    return this.isColumnFrozen(col);
  }

  get isSelectionColumnLastFrozen(): boolean {
    return !this.layout.visibleCols.some((col) => this.isColumnFrozen(col));
  }

  /** A frozen column starts after the checkbox column, when there is one. */
  frozenColumnOffset(): string {
    return this.host.selectionMode === 'multiple' ? `${this.frozenOffsetPx}px` : '0';
  }

  /**
   * Tracks the selection column's rendered width. A ResizeObserver rather than a one-off read:
   * every column drag and every window resize re-splits the shares, so the offset moves without
   * anything about the selection column itself changing.
   */
  observeSelectionWidth(cell: HTMLElement | undefined): void {
    if (!cell || this.selectionWidthObserver) return;

    const measure = () => {
      const width = cell.offsetWidth;
      if (width === this.frozenOffsetPx) return;

      this.zone.run(() => (this.frozenOffsetPx = width));
    };

    if (typeof ResizeObserver !== 'undefined') {
      this.zone.runOutsideAngular(() => {
        this.selectionWidthObserver = new ResizeObserver(measure);
        this.selectionWidthObserver.observe(cell);
      });
    }

    // Measured once immediately as well, on a microtask. The observer's first callback arrives an
    // animation frame later, and until it does the identity column sits at left:0 — on top of the
    // checkbox column it is supposed to start after. The microtask keeps the write out of the CD
    // pass that just finished, which doing it inline here would break with an NG0100.
    // The microtask can run before the first rows render, because rows land on a macrotask. When
    // those rows re-split the shares, the observer corrects the offset at frame timing, which
    // `whenStable` does not wait for. Headless Chrome on Linux re-splits by 2px where macOS does
    // not, so a green local run proves nothing. A spec that asserts the offset settles animation
    // frames first (`settleFrozenOffset` in the spec).
    queueMicrotask(measure);
  }

  /** Stops tracking the selection column, at the table's teardown. */
  disconnect(): void {
    this.selectionWidthObserver?.disconnect();
  }
}
