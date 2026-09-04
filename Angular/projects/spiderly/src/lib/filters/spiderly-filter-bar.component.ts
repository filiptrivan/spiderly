import { CommonModule } from '@angular/common';
import { Component, computed, input, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';

import {
  AppliedFilter,
  DEFAULT_OPERATOR,
  FilterBarSource,
  FilterHandle,
  FilterValueKind,
} from './filter-store';

export { FilterBarSource };

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
      <div class="filter-bar" data-testid="filter-bar">
        @for (chip of filters().applied(); track chip.id) {
          <span class="filter-chip" data-testid="filter-chip">
            <!-- The chip body reopens the filter. Once applied it is no longer offered under
                 "+ Filter", so without this the only way to narrow Elektromont to Elektro is to
                 delete the filter and rebuild it. -->
            <button
              type="button"
              class="filter-chip-edit"
              data-testid="filter-chip-edit"
              (click)="startEditing(chip.id)"
            >
              <span class="filter-chip-label">{{ chip.label }}</span>
              <span class="filter-chip-value">{{ chipValue(chip) }}</span>
            </button>
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

        @if (addable().length) {
          <button
            type="button"
            class="filter-add"
            data-testid="add-filter"
            [attr.aria-expanded]="isAddOpen()"
            (click)="isAddOpen.set(!isAddOpen())"
          >
            + {{ t('AddFilter') }}
          </button>
        }

        @if (isAddOpen()) {
          <div class="filter-add-menu" role="menu">
            @for (option of addable(); track option.id) {
              <button
                type="button"
                role="menuitem"
                class="filter-add-option"
                data-testid="add-filter-option"
                (click)="startEditing(option.id)"
              >
                {{ option.label }}
              </button>
            }
          </div>
        }

        @if (editing(); as handle) {
          <div class="filter-editor" data-testid="filter-editor">
            <span class="filter-editor-label">{{ handle.label }}</span>

            @if (handle.kind === 'text' || handle.kind === 'number') {
              <!-- A DOM control's raw value is always a string. It is coerced on the way into
                   the store (see coerce), which is the one place the value type can be got
                   right. Backticks are forbidden in this template: it is a JS template literal
                   and one terminates it, with the error landing lines away. -->
              <input
                [type]="handle.kind === 'number' ? 'number' : 'text'"
                class="filter-editor-value"
                data-testid="filter-editor-value"
                [value]="handle.value() ?? ''"
                (input)="draft(handle, $event)"
              />
            } @else {
              <!-- No control for this kind yet. A text box would write the wrong VALUE TYPE into
                   the store, which nothing checks at runtime — the operator is checked, the value
                   is not. Visibly missing beats silently wrong. -->
              <span data-testid="filter-editor-unsupported">{{
                t('FilterControlNotAvailable')
              }}</span>
            }

            <button
              type="button"
              class="filter-editor-apply"
              data-testid="filter-editor-apply"
              (click)="apply(handle)"
            >
              {{ t('Apply') }}
            </button>
          </div>
        }
      </div>
    </ng-container>
  `,
})
export class SpiderlyFilterBarComponent {
  readonly filters = input.required<FilterBarSource>();

  readonly isAddOpen = signal(false);

  /** The filter whose control is open. One at a time: the bar edits, it is not a form. */
  readonly editing = signal<FilterHandle | null>(null);

  /**
   * What "+ Filter" offers: every declared filter that is not already on the bar. Sourced from the
   * DEFINITIONS, so a filter reaches this list whether or not it has a column, and whether or not
   * that column is visible. That is the whole reason the bar exists.
   */
  readonly addable = computed(() => {
    const onBar = new Set(this.filters().applied().map((chip) => chip.id));

    return Object.entries(this.filters().definitions)
      .filter(([id]) => !onBar.has(id))
      .map(([id, definition]) => ({ id, ...definition }));
  });

  startEditing(id: string): void {
    this.isAddOpen.set(false);
    this.editing.set(this.filters().get(id));
  }

  /** Writes the draft. Nothing reaches the bar or the query until `apply`. */
  draft(handle: FilterHandle, event: Event): void {
    handle.set({
      operator: handle.operator() ?? DEFAULT_OPERATOR[handle.kind],
      value: this.coerce(handle.kind, (event.target as HTMLInputElement).value),
    });
  }

  /**
   * A DOM control hands back a string; the store holds the value type the filter declared.
   *
   * The empty case is the trap and it is not symmetric: `Number('')` is 0, so an emptied number
   * box would apply a filter for zero and draw a chip over a control the operator had just
   * cleared. `NaN` is folded in for the same reason — the store's `isBlank` cannot recognise
   * either of them as "nothing was entered", because by then they are perfectly good numbers.
   */
  private coerce(kind: FilterValueKind, raw: string): unknown {
    if (kind !== 'number') return raw;
    if (raw.trim() === '') return null;

    const parsed = Number(raw);

    return Number.isNaN(parsed) ? null : parsed;
  }

  apply(handle: FilterHandle): void {
    handle.commit();
    this.editing.set(null);
  }

  /** `In` is the multi-valued operator, so its chip reads as a list. */
  chipValue(chip: AppliedFilter): string {
    return Array.isArray(chip.value)
      ? chip.value.join(', ')
      : String(chip.value);
  }
}
