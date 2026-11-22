import { inject, NgZone } from '@angular/core';
import { ColumnLayout } from './column-layout';
import { rootFontSizePx } from './column-shares';
import { Column } from './data-table-column';

/**
 * Sets a column's width from the page: a drag of its header's grip, or a fit to its widest cell.
 * Each gives the layout a share. Construct it in the table's injection context, for `NgZone`.
 */
export class ColumnResize {
  private readonly zone = inject(NgZone);

  /** Aborts an in-flight column resize's document listeners, from `onUp` or from teardown. */
  private resizeAbort: AbortController | null = null;

  constructor(private readonly layout: ColumnLayout) {}

  /**
   * Trades share between the column being dragged and the one on its right, so the table keeps
   * its total: widening a column never starts a horizontal scroll by itself, which only happens
   * once the SUM of the minimums stops fitting. The px delta is converted through the column's
   * own rendered width, which is the only place the share-to-pixel scale is actually known.
   */
  start(col: Column, event: MouseEvent, th: HTMLElement): void {
    event.preventDefault();
    event.stopPropagation();

    const neighbour =
      this.layout.visibleCols[this.layout.visibleCols.indexOf(col) + 1];
    if (!col.field || !neighbour?.field) return;

    const controller = new AbortController();
    this.resizeAbort?.abort();
    this.resizeAbort = controller;

    const startX = event.clientX;
    const startShare = this.layout.columnShare(col);
    const neighbourShare = this.layout.columnShare(neighbour);
    const sharePerPx = startShare / Math.max(th.offsetWidth, 1);
    // A column that reaches zero share disappears with no way back from the header it lost.
    const floor = 2;

    const neighbourCell = th.nextElementSibling as HTMLElement | null;
    let applied = 0;

    const onMove = (move: MouseEvent) => {
      const delta = (move.clientX - startX) * sharePerPx;
      applied = Math.max(
        Math.min(delta, neighbourShare - floor),
        floor - startShare,
      );

      // The preview is written straight to the two header cells. Under the fixed layout their
      // widths are what lay the grid out, so this IS the live drag — and it costs no change
      // detection, where routing it through the layout's shares ran a full-grid pass per mouse move
      // (~60/sec) to move two cells. The bindings overwrite these on the next pass anyway.
      th.style.width = `${startShare + applied}rem`;
      if (neighbourCell) {
        neighbourCell.style.width = `${neighbourShare - applied}rem`;
      }
    };

    const onUp = () => {
      controller.abort();
      this.resizeAbort = null;

      this.zone.run(() =>
        this.layout.setShares({
          [col.field!]: startShare + applied,
          [neighbour.field!]: neighbourShare - applied,
        }),
      );

      // Sorting hangs off CLICK, which stopPropagation on mousedown never touched — so every
      // resize also reordered the grid. Swallowing the click on the grip is not enough either:
      // on a drag that ends away from it, the click fires on their common ancestor, the th.
      // One capturing listener, once, on the document.
      const swallow = (click: MouseEvent) => {
        click.stopPropagation();
        click.preventDefault();
      };
      document.addEventListener('click', swallow, {
        capture: true,
        once: true,
      });
      // Removed on the next tick rather than left to `once`. The click that follows a mouseup
      // fires synchronously, so this always catches it — while a drag that ends with no click at
      // all (the pointer leaves the window, the operator navigates away) would otherwise leave a
      // listener sitting on the document to eat someone's next click entirely.
      setTimeout(() =>
        document.removeEventListener('click', swallow, { capture: true }),
      );
    };

    // One controller for the pair, aborted by `onUp` AND by `abort` at teardown. Left to `removeEventListener`
    // inside `onUp` alone, navigating away mid-drag stranded `mousemove` on the document — patched
    // by zone.js, so it ticked the app on every mouse move for the rest of the session, with its
    // closure pinning the dead component and the page of rows it captured.
    this.zone.runOutsideAngular(() => {
      document.addEventListener('mousemove', onMove, {
        signal: controller.signal,
      });
      document.addEventListener('mouseup', onUp, { signal: controller.signal });
    });
  }

  /** Ends an in-flight resize's document listeners, at the table's teardown. */
  abort(): void {
    this.resizeAbort?.abort();
  }

  /**
   * Sizes the column to the widest thing in it. The cheaper escape from a cramped column: no
   * drag, no aim. Unlike a drag it does NOT trade with the neighbour — a minimum that grew is
   * exactly the case decision 4 lets push the table past its container and start scrolling.
   *
   * Measures the CELL CONTENT, not the cell: the clamp lives on the inner span, so a `td` reports
   * a scrollWidth that fits while the text inside it does not.
   */
  fit(col: Column, th: HTMLElement): void {
    // Walked up from the header rather than reached through PrimeNG's own ElementRef: the th is
    // the only thing that ties a Column to a table column in the DOM, and it already knows which
    // table it is in.
    const index = Array.from(th.parentElement.children).indexOf(th) + 1;
    const cells = th
      .closest('table')
      ?.querySelectorAll<HTMLElement>(`tbody tr > td:nth-child(${index})`);
    if (!cells?.length) return;

    // Padding comes from one CSS rule for every cell in the column, so it is read once off the
    // first — a getComputedStyle per cell was a hundred style flushes for one number.
    const styles = getComputedStyle(cells[0]);
    const padding =
      (parseFloat(styles.paddingLeft) || 0) +
      (parseFloat(styles.paddingRight) || 0);

    // Cells AND their descendants in one query rather than one per cell: the clamp lives on an
    // inner span, so a td reports a scrollWidth that fits while the text inside it does not, and
    // a consumer's cell template can nest the overflowing element arbitrarily deep.
    const content = th
      .closest('table')!
      .querySelectorAll<HTMLElement>(
        `tbody tr > td:nth-child(${index}), tbody tr > td:nth-child(${index}) *`,
      );

    let widest = th.scrollWidth;
    for (const node of Array.from(content)) {
      widest = Math.max(widest, node.scrollWidth + padding);
    }
    this.layout.setShares({ [col.field]: this.shareThatFits(col, th, widest) });
  }

  /**
   * The share that actually yields `needed` pixels — not `needed` scaled by the column's current
   * px-per-share, which is the obvious answer and the wrong one. Shares are a PROPORTION of
   * whatever width the table has, so growing this column's share also shrinks the realized width
   * of every other column, and the naive figure lands short. The spec caught it as a cell that
   * was still clipped after being told to fit.
   *
   * While the columns fit, the table keeps the container's width and the proportion has to solve
   *   needed / tableWidth = newShare / (otherShares + newShare)
   * with `otherShares` recovered from what this column currently renders at. Once `needed` is the
   * whole container the proportional regime has no answer: the table is about to overrun its
   * container and scroll (decision 4), and there a share IS its own length, so the pixels convert
   * straight through the root font size.
   */
  private shareThatFits(col: Column, th: HTMLElement, needed: number): number {
    const share = this.layout.columnShare(col);
    const thPx = Math.max(th.offsetWidth, 1);
    const tableWidth = (th.closest('table') as HTMLElement | null)?.offsetWidth ?? 0;

    if (needed < tableWidth) {
      const otherShares = (share * (tableWidth - thPx)) / thPx;

      return (needed * otherShares) / (tableWidth - needed);
    }

    return needed / rootFontSizePx();
  }
}
