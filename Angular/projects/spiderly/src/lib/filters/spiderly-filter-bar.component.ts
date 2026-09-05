import { CommonModule, formatDate } from '@angular/common';
import { FormsModule } from '@angular/forms';
import {
  Component,
  computed,
  inject,
  input,
  LOCALE_ID,
  output,
  QueryList,
  signal,
  TemplateRef,
} from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { PopoverModule } from 'primeng/popover';
import { SelectModule } from 'primeng/select';

import { SpiderlyFilterTemplateDirective } from '../directives/spiderly-filter-template.directive';
import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import { FilterValueKind } from './allowed-operators';
import {
  AppliedFilter,
  FilterBarSource,
  FilterHandle,
  FilterOption,
  SortKeyLabel,
} from './filter-store';

/** One option's word for one value — the honest raw value when no option matches (failed lookup). */
function spellOptionValue(
  options: FilterOption[] | undefined,
  value: unknown,
): string {
  return String(options?.find((option) => option.value === value)?.label ?? value);
}

/**
 * Lowercased and stripped of diacritics for matching. NFD decomposition handles č/ć/š/ž, but NOT
 * đ — it is its own letter (U+0111), not a d carrying a mark, so it decomposes to nothing and has
 * to be mapped by hand.
 *
 * It maps to "dj", which is what the rest of this workspace already does (pa-cms's
 * `normalizePlaceQuery` and `toAscii`, the backend's `PlaceNameNormalizer`) and what someone
 * without a Serbian layout actually types. Mapping it to "d" — as this first shipped — folds
 * "Đorđe" to "dorde", so typing "djordje" finds nothing: the exact case the comment claimed to
 * fix, and one no spec covered because the test used "drzava", which has no đ in it.
 */
export function foldForSearch(value: string): string {
  return value
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/\u0111/g, 'dj')
    .replace(/\u0110/g, 'Dj')
    .toLowerCase()
    .trim();
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
  imports: [
    CommonModule,
    FormsModule,
    TranslocoDirective,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    InputTextModule,
    MultiSelectModule,
    PopoverModule,
    SelectModule,
  ],
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
            pButton
            type="button"
            class="p-button-text p-button-sm filter-add"
            icon="pi pi-plus"
            [label]="t('AddFilter')"
            data-testid="add-filter"
            [attr.aria-expanded]="isAddOpen()"
            (click)="addMenu.toggle($event); isAddOpen.set(!isAddOpen())"
          ></button>
        }

        @if (filters().applied().length) {
          <!-- The bar owns clearing now: the toolbar's button sat a row away from the chips it
               cleared, which is two affordances for one job. Emits rather than clearing the store
               itself, because whoever owns the query also owns the persisted state and the sort
               that a full clear has to reach. -->
          <button
            pButton
            type="button"
            class="p-button-text p-button-sm filter-bar-clear"
            icon="pi pi-filter-slash"
            [label]="t('ClearFilters')"
            data-testid="filter-bar-clear"
            (click)="clearAll.emit()"
          ></button>
        }

        <p-popover #addMenu (onHide)="isAddOpen.set(false); addSearch.set('')">
          <div class="filter-add-menu" role="menu">
            <input
              pInputText
              pSize="small"
              type="search"
              class="filter-add-search"
              data-testid="add-filter-search"
              [value]="addSearch()"
              [attr.aria-label]="t('AddFilter')"
              [placeholder]="t('Search')"
              (input)="addSearch.set($any($event.target).value)"
            />

            @for (option of offered(); track option.id) {
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

      </div>

      <!-- Its OWN ROW, directly under the chips. It used to render at the end of the bar, where
           the clear button's auto left margin pushed everything after it to the far right, so the
           control for the second filter opened across the screen from the click (Filip, on
           /tags). A popover anchored to the click was tried first and abandoned: PrimeNG's
           ng-content slot does not re-project content once the editor's @if has destroyed it, so
           reopening gave an empty container with overlayVisible true. A fixed row is predictable,
           which is the property the complaint was actually about.
           NO BACKTICKS IN THIS TEMPLATE: it is a JS template literal and one terminates it, with
           the error landing lines away. This is the second time. -->
      @if (editing(); as handle) {
        <div class="filter-editor" data-testid="filter-editor">
            <span class="filter-editor-label">{{ handle.label }}</span>

            @if (handle.operators.length > 1) {
              <p-select
                size="small"
                styleClass="filter-editor-operator"
                data-testid="filter-editor-operator"
                [options]="operatorChoices()"
                optionLabel="label"
                optionValue="value"
                [ngModel]="handle.operator() ?? handle.defaultOperator"
                (onChange)="pickOperator(handle, $event.value)"
              ></p-select>
            }

            @if (controlTemplateFor(handle); as projected) {
              <!-- The consumer's own control for this filter. Read the QueryList live rather than
                   indexing it into a Map: a QueryList already updates itself, and a template
                   declared inside a @for or an @if would go stale in a cached index. Same
                   reasoning as getCellTemplate. -->
              <ng-container
                *ngTemplateOutlet="projected; context: { $implicit: handle }"
              ></ng-container>
            } @else if (handle.options) {
              <p-multiSelect
                size="small"
                styleClass="filter-editor-value"
                data-testid="filter-editor-value"
                [options]="handle.options"
                optionLabel="label"
                optionValue="value"
                [placeholder]="t('All')"
                [ngModel]="handle.value()"
                (onChange)="draftValue(handle, $event.value)"
              ></p-multiSelect>
            } @else if (handle.kind === 'boolean') {
              <p-checkbox
                styleClass="filter-editor-value"
                data-testid="filter-editor-value"
                [binary]="true"
                [ngModel]="handle.value() === true"
                (onChange)="draftValue(handle, $event.checked)"
              ></p-checkbox>
            } @else if (handle.kind === 'date') {
              <p-datepicker
                size="small"
                styleClass="filter-editor-value"
                data-testid="filter-editor-value"
                [ngModel]="handle.value()"
                (onSelect)="draftValue(handle, $event)"
              ></p-datepicker>
            } @else if (handle.kind === 'text' || handle.kind === 'number') {
              <!-- A DOM control's raw value is always a string. It is coerced on the way into
                   the store (see coerce), which is the one place the value type can be got
                   right. Backticks are forbidden in this template: it is a JS template literal
                   and one terminates it, with the error landing lines away. -->
              <input
                pInputText
                pSize="small"
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
            pButton
            type="button"
            class="p-button-sm filter-editor-apply"
            [label]="t('Apply')"
            data-testid="filter-editor-apply"
            (click)="apply(handle)"
          ></button>
        </div>
      }
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

  private readonly transloco = inject(TranslocoService);

  /** For the chip's date wording — the same locale mechanism the table's cells format with. */
  private readonly locale = inject(LOCALE_ID);

  /**
   * Controls a consumer projected for specific filters, collected by whoever hosts this bar. A
   * filter with no template keeps the control the bar draws for its kind, so a table opts in one
   * filter at a time.
   */
  readonly controlTemplates = input<
    QueryList<SpiderlyFilterTemplateDirective> | undefined
  >(undefined);

  readonly isAddOpen = signal(false);

  /** Cleared when the popover closes, so reopening never starts inside someone's old query. */
  readonly addSearch = signal('');

  /** The filter whose control is open. One at a time: the bar edits, it is not a form. */
  readonly editing = signal<FilterHandle | null>(null);

  /**
   * `addable` narrowed by the search box. Matching is unaccented and case-folded because that is
   * how the label gets typed: "drzava" for "Država" on any keyboard without a Serbian layout. The
   * backend's own product search is unaccented for the same reason.
   */
  readonly offered = computed(() => {
    const needle = foldForSearch(this.addSearch());
    if (!needle) return this.addable();

    return this.addable().filter((option) =>
      foldForSearch(option.label).includes(needle),
    );
  });

  /**
   * What "+ Filter" offers: every declared filter that is not already on the bar. Sourced from the
   * DEFINITIONS, so a filter reaches this list whether or not it has a column, and whether or not
   * that column is visible. That is the whole reason the bar exists.
   *
   * `offered: false` filters stay out — they have a dedicated control of their own on the page
   * (PACMS's order search box), so the generic entry point would give one question two homes.
   * Their chips still render: the applied list above reads no such flag.
   */
  readonly addable = computed(() => {
    const onBar = new Set(this.filters().applied().map((chip) => chip.id));

    return Object.entries(this.filters().definitions)
      .filter(([id, definition]) => !onBar.has(id) && definition.offered !== false)
      .map(([id, definition]) => ({ id, ...definition }));
  });

  /** Opens a filter's control. Where it renders, and why not a popover: the template comment. */
  startEditing(id: string): void {
    this.isAddOpen.set(false);
    this.editing.set(this.filters().get(id));
  }

  /** The raw-string controls only. Nothing reaches the bar or the query until `apply`. */
  draft(handle: FilterHandle, event: Event): void {
    this.draftValue(
      handle,
      this.coerce(handle.kind, (event.target as HTMLInputElement).value),
    );
  }

  controlTemplateFor(handle: FilterHandle): TemplateRef<unknown> | null {
    const match = this.controlTemplates()?.find(
      (candidate) => candidate.filterId === handle.id,
    );

    return match?.template ?? null;
  }

  /**
   * The open editor's operator options, already translated — p-select takes labels, not keys.
   *
   * A `computed`, not a method in the binding: both inputs are fixed for the life of an open
   * editor, and PrimeNG's `Select.options` setter deep-compares whatever it is handed — so a
   * freshly mapped array per change-detection pass paid for a recursive structural compare to
   * conclude nothing had changed.
   */
  readonly operatorChoices = computed(() => {
    const handle = this.editing();
    if (!handle) return [];

    return handle.operators.map((option) => ({
      label: this.transloco.translate(option.labelKey),
      value: option.value,
    }));
  });

  /**
   * The ONE write path, whatever the control. Every PrimeNG control here hands back a value of
   * the right type already — a Date from the datepicker, a boolean from the checkbox, the whole
   * array from the multiselect — so only the raw string ones need coercing, and only they go
   * through `draft`.
   */
  draftValue(handle: FilterHandle, value: unknown): void {
    handle.set({
      operator: handle.operator() ?? handle.defaultOperator,
      value,
    });
  }

  /**
   * Changing the direction re-commits nothing on its own — it rewrites the draft, so the operator
   * survives until Apply like the value does.
   */
  pickOperator(handle: FilterHandle, operator: MatchModeCodes): void {
    handle.set({ operator, value: handle.value() });
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

  /**
   * `In` is the multi-valued operator, so its chip reads as a list — in the OPTIONS' words, not
   * the wire's: "Status is one of 2, 3" narrates ids nobody chose by number. The lookup is live
   * off the definition rather than snapshotted at commit, because option lists can be filled
   * asynchronously (PACMS's admin/backend deploy race): a chip restored before the lookup answers
   * upgrades from ids to labels the moment the options land, and a failed lookup degrades to the
   * honest raw value.
   */
  chipValue(chip: AppliedFilter): string {
    // `String(date)` is the full JS toString — a GMT offset and a timezone name on a chip an
    // operator scans. Same formatDate + LOCALE_ID mechanism the table's cells use; mediumDate,
    // because shortDate's two-digit year reads badly on a chip claiming a boundary.
    if (chip.value instanceof Date) {
      return formatDate(chip.value, 'mediumDate', this.locale);
    }

    const options = this.filters().definitions[chip.id]?.options;

    return Array.isArray(chip.value)
      ? chip.value.map((value) => spellOptionValue(options, value)).join(', ')
      : spellOptionValue(options, chip.value);
  }
}
