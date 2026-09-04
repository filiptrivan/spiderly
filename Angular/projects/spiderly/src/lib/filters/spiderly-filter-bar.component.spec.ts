import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';

import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import { translocoTesting } from '../testing/spec-support.spec';
import {
  booleanFilter,
  createFilterStore,
  FilterBarSource,
  numberFilter,
  textFilter,
} from './filter-store';
import { SpiderlyFilterBarComponent } from './spiderly-filter-bar.component';

// Typed, not `unknown`: the parameter IS the assertion that a store built from `createFilterStore`
// satisfies the bar's narrow source interface without the two agreeing on filter ids.
function renderBar(filters: FilterBarSource) {
  TestBed.configureTestingModule({
    imports: [
      SpiderlyFilterBarComponent,
      TranslocoTestingModule.forRoot(translocoTesting()),
    ],
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

function startEditing(fixture: Rendered, index = 0): void {
  el(fixture)
    .querySelector<HTMLButtonElement>('[data-testid="add-filter"]')!
    .click();
  fixture.detectChanges();

  Array.from(
    el(fixture).querySelectorAll<HTMLButtonElement>(
      '[data-testid="add-filter-option"]',
    ),
  )[index].click();
  fixture.detectChanges();
}

function typeAndApply(fixture: Rendered, value: string): void {
  const input = el(fixture).querySelector<HTMLInputElement>(
    '[data-testid="filter-editor-value"]',
  )!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
  fixture.detectChanges();

  el(fixture)
    .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
    .click();
  fixture.detectChanges();
}

const chips = (fixture: { nativeElement: unknown }): HTMLElement[] =>
  Array.from(el(fixture).querySelectorAll('[data-testid="filter-chip"]'));

// The bar is the visible surface that lets a hidden column keep its filter. Everything it shows
// comes from `applied()`, so a chip can never claim a constraint the grid is not actually under.
describe('SpiderlyFilterBarComponent', () => {
  it('draws one chip per applied filter and leaves uncommitted ones off it', () => {
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

  it("removes the filter when the chip's x is clicked", () => {
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
  it('offers the filters that are not applied, and not the ones that are', () => {
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
    el(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="add-filter"]')!
      .click();
    fixture.detectChanges();

    const offered = Array.from(
      el(fixture).querySelectorAll<HTMLElement>(
        '[data-testid="add-filter-option"]',
      ),
    ).map((option) => option.textContent!.trim());

    expect(offered).toEqual(['Status']);
  });

  // End to end through the DOM: pick a filter that has no column anywhere, type a value, apply,
  // and the grid is narrowed by it. This is the path `Order.CompanyName` had none of.
  it('applies a filter picked from the list, with the default operator for its kind', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    el(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="add-filter"]')!
      .click();
    fixture.detectChanges();
    el(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="add-filter-option"]')!
      .click();
    fixture.detectChanges();

    const input = el(fixture).querySelector<HTMLInputElement>(
      '[data-testid="filter-editor-value"]',
    )!;
    input.value = 'Elektromont';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    // Still a draft: nothing on the bar until Apply.
    expect(chips(fixture).length).toBe(0);

    el(fixture)
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
        value: 'Elektromont',
      },
    ]);
    expect(chips(fixture).length).toBe(1);
  });

  // Every DOM control hands back a string. Storing it as one puts `"5"` in a numeric constraint,
  // which the paginator compares against an integer column — so the coercion belongs here, at the
  // one place the control's raw value enters the store.
  it('coerces the number control back to a number', () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    const fixture = renderBar(filters);
    startEditing(fixture);
    typeAndApply(fixture, '5');

    expect(filters.toFilterPayload()).toEqual({
      orderStatusId: [{ matchMode: MatchModeCodes.Equals, value: 5 }],
    });
  });

  // An emptied number box hands back "", and Number("") is 0. Coercing naively would apply a
  // filter for zero and draw a chip reading "0" over a control the operator had just cleared.
  it('treats an emptied number control as blank, not as zero', () => {
    const filters = createFilterStore({
      orderStatusId: numberFilter({ label: 'Status' }),
    });

    const fixture = renderBar(filters);
    startEditing(fixture);
    typeAndApply(fixture, '');

    expect(filters.applied()).toEqual([]);
    expect(filters.toFilterPayload()).toEqual({});
  });

  // An applied filter is a chip and no longer in "+ Filter", so without this the only way to
  // change Elektromont to Elektro is to remove the filter and build it again.
  it('reopens an applied filter from its chip, with its value in the control', () => {
    const filters = createFilterStore({
      companyName: textFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    startEditing(fixture);
    typeAndApply(fixture, 'Elektromont');

    expect(chips(fixture).length).toBe(1);

    chips(fixture)[0]
      .querySelector<HTMLButtonElement>('[data-testid="filter-chip-edit"]')!
      .click();
    fixture.detectChanges();

    expect(
      el(fixture).querySelector<HTMLInputElement>(
        '[data-testid="filter-editor-value"]',
      )!.value,
    ).toBe('Elektromont');
  });

  // `false` is a FILTER ("show me the ones that are not company orders"), not an empty control.
  // Every naive blank check treats it as nothing, which is why it gets its own test rather than
  // riding along with the `true` case.
  it('applies a boolean control, and false narrows rather than clearing', () => {
    const filters = createFilterStore({
      isCompanyOrder: booleanFilter({ label: 'Firma' }),
    });

    const fixture = renderBar(filters);
    startEditing(fixture);

    const checkbox = el(fixture).querySelector<HTMLInputElement>(
      '[data-testid="filter-editor-value"]',
    )!;
    checkbox.checked = false;
    checkbox.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    el(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    fixture.detectChanges();

    expect(filters.toFilterPayload()).toEqual({
      isCompanyOrder: [{ matchMode: MatchModeCodes.Equals, value: false }],
    });
    // And the chip says so in words: String(false) would put "false" in front of an operator.
    expect(chips(fixture)[0].textContent).toContain('No');
  });
});
