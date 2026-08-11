import { Directive, Input, TemplateRef } from '@angular/core';
// Type-only, so it is erased at build time and the component <-> directive cycle never exists at
// runtime.
import type { Column } from '../components/spiderly-data-table/spiderly-data-table.component';

/**
 * Context handed to a cell template. Read the row with `let-row`, the rest by name
 * (`let-value="value"`, `let-displayValue="displayValue"`, `let-col="col"`).
 *
 * `value` and `displayValue` mean here exactly what they mean on `CellClickEvent`, the other seam
 * this component hands a cell to — raw and formatted respectively. One column can use both
 * (a click handler and a template), so they must not disagree.
 */
export interface CellTemplateContext<T = any> {
  /** The full row object the cell belongs to. */
  $implicit: T;
  /** The raw cell value, `row[col.field]`. */
  value: any;
  /**
   * What the table would have rendered — the value formatted for the column's `filterType` and
   * the app's `LOCALE_ID` (dates, numbers, Yes/No). A template that only adds something around
   * the value never has to re-implement that formatting.
   */
  displayValue: string;
  /** The column being rendered. */
  col: Column<T>;
}

/**
 * Renders the cells of ONE column of a `spiderly-data-table`, replacing the plain formatted
 * value. Bind it to the column's `field`; columns with no matching template keep the built-in
 * rendering, so a table opts in one column at a time.
 *
 * Consumer guide (context, what it does and does not replace, when to prefer it over
 * `onCellClick`): the `angular-customization` doc, "Custom Cell Content".
 */
@Directive({
  selector: 'ng-template[spiderlyCellTemplate]',
})
export class SpiderlyCellTemplateDirective<T = any> {
  /** The `field` of the column whose cells this template renders. */
  @Input('spiderlyCellTemplate') field: string;

  constructor(public readonly template: TemplateRef<CellTemplateContext<T>>) {}

  /**
   * Without this the `let-` variables are `any` at every consumer, however well
   * {@link CellTemplateContext} is declared — a directive's injected `TemplateRef<C>` never
   * reaches the template that declares it. Same mechanism as `SpiderlyTemplateTypeDirective`.
   */
  static ngTemplateContextGuard<T>(
    dir: SpiderlyCellTemplateDirective<T>,
    ctx: unknown,
  ): ctx is CellTemplateContext<T> {
    return true;
  }
}
