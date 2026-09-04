import { TestBed } from '@angular/core/testing';
import { TranslocoTestingModule } from '@jsverse/transloco';

import { MatchModeCodes } from '../enums/match-mode-enum-codes';
import { translocoTesting } from '../testing/spec-support.spec';
import {
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

const chips = (fixture: { nativeElement: HTMLElement }): HTMLElement[] =>
  Array.from(fixture.nativeElement.querySelectorAll('[data-testid="filter-chip"]'));

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
});
