import { CommonModule } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { TranslocoDirective } from '@jsverse/transloco';
import { Popover, PopoverModule } from 'primeng/popover';

import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import {
  AppliedFilter,
  FilterBarSource,
  FilterHandle,
  FilterOption,
  FilterValueKind,
  SortKeyLabel,
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
  imports: [CommonModule, TranslocoDirective, PopoverModule],
  styleUrl: 'spiderly-filter-bar.component.scss',
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
              <!-- &ngsp; is a space the template parser keeps. Angular collapses whitespace
                   between elements by default, which glued these three into
                   "FirmacontainsElektromont" — how a screen reader reads the chip and how it
                   lands when copied, not merely how the spec saw it. The flex gap only spaced
                   them visually. -->
              <span class="filter-chip-label">{{ chip.label }}</span>&ngsp;
              <span class="filter-chip-operator">{{
                t(chip.operatorPhraseKey)
              }}</span>&ngsp;
              <span class="filter-chip-value">{{
                chip.kind === 'boolean'
                  ? chip.value
                    ? t('Yes')
                    : t('No')
                  : chipValue(chip)
              }}</span>
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

        @if (sort().length) {
          <!-- Read-only, and that is the scope on purpose. Multi-sort is invisible today (sort
               icons scattered over headers, nothing saying which is primary), so saying it in one
               place is the whole gain. An x here would have to mean "back to the DEFAULT sort"
               rather than "no sort", because applyDefaultSortIfUnsorted puts the default straight
               back — an affordance that lies is worse than none. -->
          <span class="sort-chip" data-testid="sort-chip">
            <span class="sort-chip-label">{{ t('SortedBy') }}</span>
            @for (key of sort(); track key.label; let last = $last) {
              <span class="sort-chip-key"
                >{{ key.label }} {{ key.descending ? '↓' : '↑' }}{{
                  last ? '' : ','
                }}</span
              >
            }
          </span>
        }

        @if (totalRecords() !== null) {
          <!-- What came back, next to the chips that say what was asked. An empty grid under a
               filter is otherwise a mystery. The CURRENT query's count only: the unfiltered total
               would cost a second request the paginator does not offer. -->
          <span class="filter-bar-count" data-testid="filter-bar-count">{{
            t('ResultCount', { count: totalRecords() })
          }}</span>
        }

        @if (addable().length) {
          <!-- A popover, not an inline list. Seven filters already fill the toolbar row inline,
               and the orders grid declares nineteen — they would wrap into three lines and shove
               the table down the page. Shopify's Filters component opens its "more filters" the
               same way. The teleport into document.body costs the specs a container lookup; that
               cost belongs to the tests, not to the operator. -->
          <button
            type="button"
            class="filter-add"
            data-testid="add-filter"
            [attr.aria-expanded]="isAddOpen()"
            (click)="addMenu.toggle($event); isAddOpen.set(!isAddOpen())"
          >
            + {{ t('AddFilter') }}
          </button>
        }

        @if (filters().applied().length) {
          <!-- The bar owns clearing now: the toolbar's button sat a row away from the chips it
               cleared, which is two affordances for one job. Emits rather than clearing the store
               itself, because whoever owns the query also owns the persisted state and the sort
               that a full clear has to reach. -->
          <button
            type="button"
            class="filter-bar-clear"
            data-testid="filter-bar-clear"
            (click)="clearAll.emit()"
          >
            {{ t('ClearFilters') }}
          </button>
        }

        <p-popover #addMenu (onHide)="isAddOpen.set(false)">
          <div class="filter-add-menu" role="menu">
            @for (option of addable(); track option.id) {
              <button
                type="button"
                role="menuitem"
                class="filter-add-option"
                data-testid="add-filter-option"
                (click)="startEditing(option.id); addMenu.hide()"
              >
                {{ option.label }}
              </button>
            }
          </div>
        </p-popover>

        @if (editing(); as handle) {
          <div class="filter-editor" data-testid="filter-editor">
            <span class="filter-editor-label">{{ handle.label }}</span>

            @if (handle.operators.length > 1) {
              <select
                class="filter-editor-operator"
                data-testid="filter-editor-operator"
                [value]="handle.operator() ?? defaultOperator(handle)"
                (change)="pickOperator(handle, $event)"
              >
                @for (option of handle.operators; track option.value) {
                  <option [value]="option.value">{{ t(option.labelKey) }}</option>
                }
              </select>
            }

            @if (handle.options) {
              @for (option of handle.options; track option.value) {
                <label class="filter-editor-tick">
                  <input
                    type="checkbox"
                    data-testid="filter-editor-option"
                    [checked]="isTicked(handle, option)"
                    (change)="toggleOption(handle, option, $event)"
                  />
                  {{ option.label }}
                </label>
              }
            } @else if (handle.kind === 'boolean') {
              <input
                type="checkbox"
                class="filter-editor-value"
                data-testid="filter-editor-value"
                [checked]="handle.value() === true"
                (change)="draftBoolean(handle, $event)"
              />
            } @else if (handle.kind === 'date') {
              <input
                type="date"
                class="filter-editor-value"
                data-testid="filter-editor-value"
                [value]="dateInputValue(handle)"
                (input)="draft(handle, $event)"
              />
            } @else if (handle.kind === 'text' || handle.kind === 'number') {
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

  /** The current query's row count, supplied by whoever owns the query. Null hides the counter. */
  readonly totalRecords = input<number | null>(null);

  /**
   * What the grid is ordered by, in priority order and already LABELLED — the bar knows nothing
   * about columns, so whoever owns the sort resolves the names.
   */
  readonly sort = input<SortKeyLabel[]>([]);

  /** Asked for, never done here — see the button's comment. */
  readonly clearAll = output<void>();

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
      operator: handle.operator() ?? handle.defaultOperator,
      value: this.coerce(handle.kind, (event.target as HTMLInputElement).value),
    });
  }

  defaultOperator(handle: FilterHandle): MatchModeCodes {
    return handle.defaultOperator;
  }

  isTicked(handle: FilterHandle, option: FilterOption): boolean {
    const value = handle.value();

    return Array.isArray(value) && value.includes(option.value);
  }

  /**
   * A pick-list's value is the LIST, so each tick rewrites the whole draft. Untick everything and
   * the draft is an empty array, which the store already treats as blank — no chip, no key, rather
   * than an `In ()` the paginator would have to answer.
   */
  toggleOption(handle: FilterHandle, option: FilterOption, event: Event): void {
    const current = handle.value();
    const ticked = Array.isArray(current) ? [...current] : [];
    const next = (event.target as HTMLInputElement).checked
      ? [...ticked, option.value]
      : ticked.filter((value) => value !== option.value);

    handle.set({
      operator: handle.operator() ?? handle.defaultOperator,
      value: next,
    });
  }

  /**
   * Changing the direction re-commits nothing on its own — it rewrites the draft, so the operator
   * survives until Apply like the value does.
   */
  pickOperator(handle: FilterHandle, event: Event): void {
    handle.set({
      operator: (event.target as HTMLSelectElement).value as MatchModeCodes,
      value: handle.value(),
    });
  }

  /** An input[type=date] reads and writes "YYYY-MM-DD" in LOCAL time. */
  dateInputValue(handle: FilterHandle): string {
    const value = handle.value();
    if (!(value instanceof Date) || Number.isNaN(value.getTime())) return '';

    const pad = (part: number) => String(part).padStart(2, '0');

    return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
  }

  /**
   * The checkbox writes its own draft: its value is `checked`, not `value`. And `false` is a
   * constraint here ("not a company order"), never an empty control.
   */
  draftBoolean(handle: FilterHandle, event: Event): void {
    handle.set({
      operator: handle.operator() ?? handle.defaultOperator,
      value: (event.target as HTMLInputElement).checked,
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
    if (kind === 'date') {
      // Built from parts, never `new Date(raw)`: that parses a bare "YYYY-MM-DD" as UTC, so in
      // Belgrade "before 1 Sep" would include two hours of the 1st. A person picking a day in a
      // date box means that day where they are standing.
      if (raw === '') return null;

      const [year, month, day] = raw.split('-').map(Number);

      return new Date(year, month - 1, day);
    }

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
