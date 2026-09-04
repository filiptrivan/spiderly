import { Directive, Input, TemplateRef } from '@angular/core';
// Type-only, so it is erased at build time and the component <-> directive cycle never exists at
// runtime.
import type { FilterHandle } from '../filters/filter-store';

/**
 * Context handed to a filter template: the filter's handle, and nothing else it does not already
 * carry. Read it with `let-f` and drive the filter through it — `f.value()` is the draft the
 * control shows, `f.set(...)` writes one, `f.commit()` applies it.
 */
export interface FilterTemplateContext {
  $implicit: FilterHandle;
}

/**
 * Renders the editor for ONE filter of a `spiderly-data-table`'s bar, replacing the control the
 * bar would have drawn for that filter's kind. Bind it to the filter's id; filters with no
 * matching template keep the built-in control, so a table opts in one filter at a time.
 *
 * This is the narrow job the directive has. Placing a filter ANYWHERE else — a drawer, a modal,
 * a header cell — needs no directive at all: `store.get(id)` returns the same handle and depends
 * on nothing in the component tree, which is why the store is the consumer's to own.
 */
@Directive({
  selector: 'ng-template[spiderlyFilterTemplate]',
})
export class SpiderlyFilterTemplateDirective {
  /** The id of the filter whose control this template renders. */
  @Input('spiderlyFilterTemplate') filterId: string;

  constructor(public readonly template: TemplateRef<FilterTemplateContext>) {}

  /**
   * Without this the `let-` variable is `any` at every consumer, however well the context is
   * declared — a directive's injected `TemplateRef<C>` never reaches the template that declares
   * it. Same mechanism as `SpiderlyCellTemplateDirective`.
   */
  static ngTemplateContextGuard(
    dir: SpiderlyFilterTemplateDirective,
    ctx: unknown,
  ): ctx is FilterTemplateContext {
    return true;
  }
}
