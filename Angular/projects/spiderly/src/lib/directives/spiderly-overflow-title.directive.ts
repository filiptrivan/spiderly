import {
  AfterViewChecked,
  Directive,
  ElementRef,
  NgZone,
  OnDestroy,
  inject,
} from '@angular/core';

/**
 * Puts a native `title` on an element ONLY while its text is actually clipped.
 *
 * The data table clamps a default cell to one line (see the `.cell-text` rule), which hides the
 * value and, before this, offered nothing to read it with — the reason PACMS hand-added `[title]`
 * on four cells of one grid. Wrapping the column is a choice someone makes; this covers the
 * default nobody touched.
 *
 * Native `title`, never `pTooltip`: it needs a deliberate hover rather than firing as the cursor
 * crosses a dense list, which is what got the SKU tooltip removed. And only on overflow, because
 * a title on a cell that fits is noise on every one of those hovers.
 */
@Directive({
  selector: '[spiderlyOverflowTitle]',
})
export class SpiderlyOverflowTitleDirective
  implements AfterViewChecked, OnDestroy
{
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly zone = inject(NgZone);

  /** The text last measured, so a check costs a string compare rather than a reflow. */
  private measured: string | null = null;

  private observer?: ResizeObserver;

  ngAfterViewChecked(): void {
    const text = this.el.nativeElement.textContent ?? '';
    if (text === this.measured) return;

    this.measured = text;
    this.sync();
    this.observeWidth();
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  /**
   * A column can be resized or the window narrowed without the TEXT changing, and either can turn
   * a cell that fit into one that does not. Outside the zone: this fires per column drag frame
   * across every cell, and it changes an attribute rather than any bound state.
   */
  private observeWidth(): void {
    if (this.observer || typeof ResizeObserver === 'undefined') return;

    this.zone.runOutsideAngular(() => {
      this.observer = new ResizeObserver(() => this.sync());
      this.observer.observe(this.el.nativeElement);
    });
  }

  private sync(): void {
    const el = this.el.nativeElement;

    // A one-pixel slack: sub-pixel text metrics make scrollWidth exceed clientWidth by fractions
    // on text that visibly fits, which would title half the grid.
    if (el.scrollWidth - el.clientWidth > 1) {
      el.setAttribute('title', el.textContent ?? '');
    } else {
      el.removeAttribute('title');
    }
  }
}
