import { Directive } from '@angular/core';

/**
 * Marks an `<ng-template>` projected into `spiderly-data-table` as custom toolbar
 * content. The template is rendered in the table's caption action area, ahead of the
 * built-in Clear Filters / Export to Excel / Reload / Delete Selected buttons, so its
 * position stays stable regardless of selection state.
 *
 * The projected template binds to the consumer component (so `(click)` handlers call
 * the consumer's methods), and inherits the action row's spacing and responsive
 * stacking. No table state is passed as context — read it via the table's outputs
 * (`onLazyLoad`, `onTotalRecordsChange`, `onRowSelect`) when needed.
 *
 * @example
 * ```html
 * <spiderly-data-table [cols]="cols" [getPaginatedListObservableMethod]="...">
 *   <ng-template spiderlyDataTableActions>
 *     <button pButton class="p-button-outlined" style="flex: none"
 *             icon="pi pi-check" [label]="t('ApproveAll')" (click)="approveAll()"></button>
 *   </ng-template>
 * </spiderly-data-table>
 * ```
 */
@Directive({
  selector: '[spiderlyDataTableActions]',
})
export class SpiderlyDataTableActionsDirective {}
