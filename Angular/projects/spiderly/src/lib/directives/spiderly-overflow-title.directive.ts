import { Directive, ElementRef, NgZone, OnDestroy, inject } from '@angular/core';

/**
 * Puts a native `title` on an element while its text is clipped — measured when the pointer
 * arrives, which is the only moment a native title can surface.
 *
 * The data table clamps a default cell to one line (see the `.cell-text` rule), which hides the
 * value and, before this, offered nothing to read it with — the reason PACMS hand-added `[title]`
 * on four cells of one grid. Wrapping the column is a choice someone makes; this covers the
 * default nobody touched.
 *
 * Native `title`, never `pTooltip`: it needs a deliberate hover rather than firing as the cursor
 * crosses a dense list, which is what got the SKU tooltip removed.
 *
 * **Measured on hover, not watched.** The first shape ran `ngAfterViewChecked` on every instance
 * every change-detection cycle (reading `textContent`, which allocates a fresh string each call)
 * and gave each instance its OWN `ResizeObserver`. On a 100-row grid nineteen columns wide that is
 * ~1900 hooks per cycle and ~1900 observers in Chrome's per-frame loop — all to decide something
 * that can only ever be seen one cell at a time. Hover is exactly-in-time, costs nothing until it
 * happens, and needs no separate handling for a column drag or a resized window: the next hover
 * re-measures. Nothing is lost for a screen reader either — CSS clipping never hid the text from
 * the accessibility tree, so the title was only ever a sighted-hover affordance.
 */
@Directive({
  selector: '[spiderlyOverflowTitle]',
})
export class SpiderlyOverflowTitleDirective implements OnDestroy {
  private readonly el = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly zone = inject(NgZone);

  private readonly onPointerEnter = () => this.sync();

  constructor() {
    // Outside the zone: it sets an attribute and touches no bound state, so waking change
    // detection on every cell the cursor crosses would be pure cost.
    this.zone.runOutsideAngular(() =>
      this.el.nativeElement.addEventListener(
        'pointerenter',
        this.onPointerEnter,
      ),
    );
  }

  ngOnDestroy(): void {
    this.el.nativeElement.removeEventListener(
      'pointerenter',
      this.onPointerEnter,
    );
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
