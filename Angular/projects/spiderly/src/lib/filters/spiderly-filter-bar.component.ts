import { CommonModule } from '@angular/common';
import { Component, input, Signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

import { AppliedFilter } from './filter-store';

/**
 * All the bar needs from a store, and deliberately no more: it reads the applied set and removes
 * from it. Narrowing it here is what keeps the bar independent of the store's generics, so it can
 * render any store without the two types having to agree on the filter ids.
 */
export interface FilterBarSource {
  applied: Signal<AppliedFilter[]>;
  reset(id: string): void;
}

/**
 * The visible home of every applied constraint. This is what licenses a hidden column keeping its
 * filter: the header is no longer the only place a filter can be seen, so hiding a column stops
 * meaning "silently drop what it was filtering by".
 *
 * Everything drawn here comes from `applied()`, never from a draft — a chip over a half-typed
 * value would repeat the mistake the header's filter icon shipped, claiming the grid is narrowed
 * while it still shows every row.
 */
@Component({
  selector: 'spiderly-filter-bar',
  imports: [CommonModule, TranslocoDirective],
  template: `
    <ng-container *transloco="let t">
      @if (filters().applied().length) {
        <div class="filter-bar" data-testid="filter-bar">
          @for (chip of filters().applied(); track chip.id) {
            <span class="filter-chip" data-testid="filter-chip">
              <span class="filter-chip-label">{{ chip.label }}</span>
              <span class="filter-chip-value">{{ chipValue(chip) }}</span>
              <button
                type="button"
                class="filter-chip-remove"
                data-testid="filter-chip-remove"
                [attr.aria-label]="t('RemoveFilter')"
                (click)="filters().reset(chip.id)"
              >
                &times;
              </button>
            </span>
          }
        </div>
      }
    </ng-container>
  `,
})
export class SpiderlyFilterBarComponent {
  readonly filters = input.required<FilterBarSource>();

  /** `In` is the multi-valued operator, so its chip reads as a list. */
  chipValue(chip: AppliedFilter): string {
    return Array.isArray(chip.value)
      ? chip.value.join(', ')
      : String(chip.value);
  }
}
