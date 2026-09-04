import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { Checkbox } from 'primeng/checkbox';
import { DatePicker } from 'primeng/datepicker';
import { MultiSelect } from 'primeng/multiselect';
import { Popover } from 'primeng/popover';
import { Select } from 'primeng/select';

import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import { translocoTesting } from '../testing/spec-support.spec';
import {
  booleanFilter,
  dateFilter,
  createFilterStore,
  FilterBarSource,
  numberFilter,
  textFilter,
} from './filter-store';
import { SpiderlyFilterBarComponent } from './spiderly-filter-bar.component';

// Typed, not `unknown`: the parameter IS the assertion that a store built from `createFilterStore`
// satisfies the bar's narrow source interface without the two agreeing on filter ids.
function renderBar(filters: FilterBarSource): ComponentFixture<SpiderlyFilterBarComponent> {
  TestBed.configureTestingModule({
    imports: [
      SpiderlyFilterBarComponent,
      // Real words for the chip phrases, so the assertions read as the sentence an operator sees
      // rather than as a key. Everything else falls back to the shared empty map.
      TranslocoTestingModule.forRoot({
        ...translocoTesting(),
        langs: {
          en: {
            FilterChipContains: 'contains',
            FilterChipIn: 'is one of',
            FilterChipEquals: 'is',
            FilterChipBefore: 'before',
          },
        },
      }),
    ],
    providers: [provideNoopAnimations()],
  });

  const fixture = TestBed.createComponent(SpiderlyFilterBarComponent);
  fixture.componentRef.setInput('filters', filters);
  fixture.detectChanges();

  return fixture;
}

const el = (fixture: { nativeElement: unknown }): HTMLElement =>
  fixture.nativeElement as HTMLElement;

// Pick a filter from "+ Filter" and open its control.
type Rendered = { nativeElement: unknown; detectChanges(): void };

// Once open, PrimeNG appends the popover to document.body, where stale ones from earlier fixtures
// linger — so query THIS fixture's Popover instance, never the document. Same rule the column
// chooser's specs already follow (spiderly-data-table/CLAUDE.md).
// There is more than one popover on the bar now, so find the container by what it HOLDS rather
// than by position — an index would silently follow whichever was declared first.
function overlay(fixture: Rendered, selector: string): HTMLElement {
  const containers = (fixture as unknown as ComponentFixture<unknown>).debugElement
    .queryAll(By.directive(Popover))
    .map((debugEl) => (debugEl.componentInstance as Popover).container)
    .filter(Boolean) as HTMLElement[];

  return containers.find((container) => container.querySelector(selector))!;
}

function addMenu(fixture: Rendered): HTMLElement {
  return overlay(fixture, '[data-testid="add-filter-option"]');
}

// In-template, unlike the add menu: the editor is a row under the chips, not an overlay.
function editor(fixture: Rendered): HTMLElement {
  return el(fixture).querySelector<HTMLElement>('[data-testid="filter-editor"]')!;
}

/**
 * The four PrimeNG controls render their own markup and open their lists in body-teleported
 * overlays, so these drive the control's OUTPUT — the same event a click produces, and the seam
 * this component actually consumes. Their internal DOM is PrimeNG's to change; what must not
 * change is what the control hands us and what the store does with it.
 */
function control<T>(fixture: Rendered, type: new (...args: never[]) => T): T {
  return (fixture as unknown as ComponentFixture<unknown>).debugElement.query(
    By.directive(type as never),
  ).componentInstance as T;
}

async function settle(fixture: Rendered): Promise<void> {
  fixture.detectChanges();
  await new Promise((resolve) => setTimeout(resolve, 50));
  await (fixture as unknown as ComponentFixture<unknown>).whenStable();
  fixture.detectChanges();
}

// Async because the popover renders its container a tick after toggle() — the same ritual
// openChooser follows in the data table's specs.
async function openAddMenu(fixture: Rendered): Promise<void> {
  el(fixture)
    .querySelector<HTMLButtonElement>('[data-testid="add-filter"]')!
    .click();
  fixture.detectChanges();
  await (fixture as unknown as ComponentFixture<unknown>).whenStable();
  fixture.detectChanges();
}

async function startEditing(fixture: Rendered, index = 0): Promise<void> {
  await openAddMenu(fixture);

  Array.from(
    addMenu(fixture).querySelectorAll<HTMLButtonElement>(
      '[data-testid="add-filter-option"]',
    ),
  )[index].click();
  fixture.detectChanges();
  await (fixture as unknown as ComponentFixture<unknown>).whenStable();
  fixture.detectChanges();
}

function typeAndApply(fixture: Rendered, value: string): void {
  const input = editor(fixture).querySelector<HTMLInputElement>(
    '[data-testid="filter-editor-value"]',
  )!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();

  editor(fixture)
    .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
    .click();
  fixture.detectChanges();
}

const chips = (fixture: { nativeElement: unknown }): HTMLElement[] =>
  Array.from(el(fixture).querySelectorAll('[data-testid="filter-chip"]'));

// The bar is the visible surface that lets a hidden column keep its filter. Everything it shows
// comes from `applied()`, so a chip can never claim a constraint the grid is not actually under.
describe('SpiderlyFilterBarComponent', () => {
  it('draws one chip per applied filter and leaves uncommitted ones off it', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    // Set but never committed: the operator is still typing, so no chip.
    filters.set('orderStatusId', {
      operator: MatchModeCodes.In,
      value: [2, 3],
    });

    const fixture = renderBar(filters);

    expect(chips(fixture).length).toBe(1);
    expect(chips(fixture)[0].textContent).toContain('Firma');
    expect(chips(fixture)[0].textContent).toContain('Elektromont');
  });

  it("removes the filter when the chip's x is clicked", async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    const fixture = renderBar(filters);
    chips(fixture)[0]
      .querySelector<HTMLButtonElement>('[data-testid="filter-chip-remove"]')!
      .click();
    fixture.detectChanges();

    expect(filters.applied()).toEqual([]);
    expect(chips(fixture).length).toBe(0);
  });

  // "+ Filter" is what makes a filter reachable without a column, which is the whole point: the
  // firm a row prints on 82% of company orders has no column and no search, so the only way to
  // ask for it is a list that offers every DECLARED filter, not every visible one.
  it('offers the filters that are not applied, and not the ones that are', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    filters.set('companyName', {
      operator: MatchModeCodes.Contains,
      value: 'Elektromont',
    });
    filters.commit('companyName');

    const fixture = renderBar(filters);
    await openAddMenu(fixture);

    const offered = Array.from(
      addMenu(fixture).querySelectorAll<HTMLElement>(
        '[data-testid="add-filter-option"]',
      ),
    ).map((option) => option.textContent!.trim());

    expect(offered).toEqual(['Status']);
  });

  // End to end through the DOM: pick a filter that has no column anywhere, type a value, apply,
  // and the grid is narrowed by it. This is the path `Order.CompanyName` had none of.
  it('applies a filter picked from the list, with the default operator for its kind', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);

    const input = editor(fixture).querySelector<HTMLInputElement>(
      '[data-testid="filter-editor-value"]',
    )!;
    input.value = 'Elektromont';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    // Still a draft: nothing on the bar until Apply.
    expect(chips(fixture).length).toBe(0);

    editor(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    fixture.detectChanges();

    expect(filters.applied()).toEqual([
      {
        id: 'companyName',
        label: 'Firma',
        kind: 'text',
        // `Contains` is the text default: an operator nobody chose has to be the one a person
        // means by typing a fragment into a box.
        operator: MatchModeCodes.Contains,
        operatorPhraseKey: 'FilterChipContains',
        value: 'Elektromont',
      },
    ]);
    expect(chips(fixture).length).toBe(1);
  });

  // Every DOM control hands back a string. Storing it as one puts `"5"` in a numeric constraint,
  // which the paginator compares against an integer column — so the coercion belongs here, at the
  // one place the control's raw value enters the store.
  it('coerces the number control back to a number', async () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);
    typeAndApply(fixture, '5');

    expect(filters.toFilterPayload()).toEqual({
      orderStatusId: [{ matchMode: MatchModeCodes.Equals, value: 5 }],
    });
  });

  // An emptied number box hands back "", and Number("") is 0. Coercing naively would apply a
  // filter for zero and draw a chip reading "0" over a control the operator had just cleared.
  it('treats an emptied number control as blank, not as zero', async () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);
    typeAndApply(fixture, '');

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});
  });

  // An applied filter is a chip and no longer in "+ Filter", so without this the only way to
  // change Elektromont to Elektro is to remove the filter and build it again.
  it('reopens an applied filter from its chip, with its value in the control', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);
    typeAndApply(fixture, 'Elektromont');

    expect(chips(fixture).length).toBe(1);

    chips(fixture)[0]
      .querySelector<HTMLButtonElement>('[data-testid="filter-chip-edit"]')!
      .click();
    fixture.detectChanges();

    expect(
      editor(fixture).querySelector<HTMLInputElement>(
        '[data-testid="filter-editor-value"]',
      )!.value,
    ).toBe('Elektromont');
  });

  // `false` is a FILTER ("show me the ones that are not company orders"), not an empty control.
  // Every naive blank check treats it as nothing, which is why it gets its own test rather than
  // riding along with the `true` case.
  it('applies a boolean control, and false narrows rather than clearing', async () => {
    const filters = createFilterStore({
      isCompanyOrder: booleanFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);

    control(fixture, Checkbox).onChange.emit({
      originalEvent: new Event('change'),
      checked: false,
    });
    fixture.detectChanges();

    editor(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    fixture.detectChanges();

    expect(filters.toFilterPayload()).toEqual({
      isCompanyOrder: [{ matchMode: MatchModeCodes.Equals, value: false }],
    });
    // And the chip says so in words: String(false) would put "false" in front of an operator.
    expect(chips(fixture)[0].textContent).toContain('No');
  });

  // Two things at once, because they only fail together: the direction has to be pickable (a date
  // filter is useless if it can only ever mean "after"), and the control's "2026-09-01" has to
  // become the LOCAL midnight a person means. new Date("2026-09-01") parses as UTC, which in
  // Belgrade is 02:00 on the 1st — two hours of rows on the wrong side of "before".
  it('applies a date in the chosen direction, at local midnight', async () => {
    const filters = createFilterStore({
      createdAt: dateFilter({ label: 'Datum' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);

    control(fixture, Select).onChange.emit({
      originalEvent: new Event('change'),
      value: MatchModeCodes.LessThan,
    });
    fixture.detectChanges();

    // The datepicker hands back a real Date at local midnight; nothing parses a string here.
    control(fixture, DatePicker).onSelect.emit(new Date(2026, 8, 1));
    fixture.detectChanges();

    editor(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    fixture.detectChanges();

    expect(filters.toFilterPayload()).toEqual({
      createdAt: [
        { matchMode: MatchModeCodes.LessThan, value: new Date(2026, 8, 1) },
      ],
    });
  });

  // The only path by which orderStatusId can exist: "Processing OR PreparingForShipping" is the
  // question asked before every bulk action, and only `In` expresses it. Declaring options is what
  // asks for it — the same rule as the table's [filters], where the shape of the input is the
  // switch rather than a mode flag.
  it('sends In over the ticked options when a filter declares them', async () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({
        label: 'Status',
        options: [
          { value: 2, label: 'U pripremi' },
          { value: 3, label: 'Spremna' },
        ],
      }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);

    const picker = control(fixture, MultiSelect);
    expect(picker.options!.length).toBe(2);

    picker.onChange.emit({ originalEvent: new Event('change'), value: [2, 3] });
    fixture.detectChanges();

    editor(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    fixture.detectChanges();

    expect(filters.toFilterPayload()).toEqual({
      orderStatusId: [{ matchMode: MatchModeCodes.In, value: [2, 3] }],
    });
  });

  // `In` needs a list of values, and the editor only draws one when the filter declares options.
  // Offering it on a plain number filter hands the operator a mode with no control behind it.
  it('does not offer In on a number filter that declares no options', async () => {
    const filters = createFilterStore({
      total: numberFilter({ label: 'Iznos' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);

    const offered = (
      control(fixture, Select).options as { value: MatchModeCodes }[]
    ).map((option) => option.value);

    expect(offered).toEqual([
      MatchModeCodes.Equals,
      MatchModeCodes.GreaterThan,
      MatchModeCodes.LessThan,
    ]);
  });

  // "Firma Elektromont" leaves the operator to be guessed at, and a bar whose whole claim is that
  // it cannot lie must not omit the half that says HOW the grid is narrowed. Contains and
  // StartsWith return very different sets for the same typed value.
  it('spells the operator on the chip', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    await startEditing(fixture);
    typeAndApply(fixture, 'Elektromont');

    expect(chips(fixture)[0].textContent!.replace(/\s+/g, ' ')).toContain(
      'Firma contains Elektromont',
    );
  });

  // Nineteen filters is a list nobody scans. Typing has to reach them the way an operator would
  // actually type — lowercase and without diacritics, because "drzava" is what gets typed for
  // "Država" on any keyboard and the backend's own search is unaccented too.
  it('narrows the offered filters as you type, ignoring case and diacritics', async () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
      orderStatusId: numberFilter({ label: 'Status' }),
      shippingCountry: textFilter({ label: 'Država isporuke' }),
    });

    const fixture = renderBar(filters);
    await openAddMenu(fixture);

    const search = addMenu(fixture).querySelector<HTMLInputElement>(
      '[data-testid="add-filter-search"]',
    )!;

    const offered = () =>
      Array.from(
        addMenu(fixture).querySelectorAll<HTMLElement>(
          '[data-testid="add-filter-option"]',
        ),
      ).map((option) => option.textContent!.trim());

    search.value = 'STA';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(offered()).toEqual(['Status']);

    search.value = 'drzava';
    search.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    expect(offered()).toEqual(['Država isporuke']);
  });
});
