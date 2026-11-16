// The table's own slots and inputs: the projected toolbar actions and filter controls, the
// filter surface, Clear filters, and the store a client-side table refuses.

import { Component } from '@angular/core';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { ColumnFilter } from 'primeng/table';
import { renderRows } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import {
  SpiderlyDataTableActionsDirective,
} from '../../directives/spiderly-data-table-actions.directive';
import {
  SpiderlyFilterTemplateDirective,
} from '../../directives/spiderly-filter-template.directive';
import { createFilterStore, numberFilter, textFilter } from '../../filters/filter-store';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  createFixture,
  createWithDataTable,
  headerSort,
  HostWithoutActionsComponent,
  columnMenu,
  openColumnMenu,
  VIEW_SORT_STATE_KEY,
  HostWithViewSortComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyDataTableActionsDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template spiderlyDataTableActions>
        <button type="button" data-testid="custom-action">Custom</button>
      </ng-template>
    </spiderly-data-table>
  `,
})
class HostWithActionsComponent {
  cols = cols;
  getList = emptyList;
}

// ---------------------------------------------------------------------------
// The filter surface is the consumer's store, rendered as the chip bar. A table handed none has
// no filter surface at all — the legacy Column.filterType header filters were deleted once no
// consumer passed the old shape. See `docs/data-table-operator-owned-view.md`.
// ---------------------------------------------------------------------------

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithFilterStoreComponent {
  cols = cols;
  getList = emptyList;
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyFilterTemplateDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template spiderlyFilterTemplate="name" let-f>
        <input
          data-testid="custom-filter"
          [value]="f.value() ?? \'\'"
          (input)="f.set({ operator: matchMode, value: $any($event.target).value })"
        />
      </ng-template>
    </spiderly-data-table>
  `,
})
class HostWithFilterTemplateComponent {
  matchMode = MatchModeCodes.Contains;
  filters = createFilterStore({
    name: textFilter({ label: 'Naziv' }),
    id: numberFilter({ label: 'Id' }),
  });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text', filterId: 'name' },
    { name: 'Id', field: 'id', filterType: 'numeric', filterId: 'id' },
  ];
  getList = emptyList;
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [hasLazyLoad]="false"
      [getFormArrayItems]="getItems"
    ></spiderly-data-table>
  `,
})
class HostClientSideWithStoreComponent {
  cols = cols;
  getItems = () => [{ id: 1 }];
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — toolbar actions projection slot', () => {
  it('renders the projected actions ahead of the built-in Export to Excel button', () => {
    const el: HTMLElement = createFixture(HostWithActionsComponent).nativeElement;

    const custom = el.querySelector('[data-testid="custom-action"]');
    const exportToExcel = el.querySelector('.pi-download')?.closest('button');

    expect(custom)
      .withContext('projected action button should render')
      .toBeTruthy();
    expect(exportToExcel)
      .withContext('built-in Export to Excel button should render')
      .toBeTruthy();

    // DOCUMENT_POSITION_FOLLOWING means exportToExcel appears *after* custom in DOM order.
    const relativePosition = custom!.compareDocumentPosition(exportToExcel!);
    expect(relativePosition & Node.DOCUMENT_POSITION_FOLLOWING)
      .withContext('projected action should precede Export to Excel')
      .toBeTruthy();
  });

  it('renders nothing extra when no actions template is projected', () => {
    const el: HTMLElement = createFixture(HostWithoutActionsComponent).nativeElement;

    expect(el.querySelector('[data-testid="custom-action"]')).toBeNull();
    // The built-in toolbar still renders.
    expect(el.querySelector('.pi-download')).toBeTruthy();
  });
});

describe('SpiderlyDataTableComponent — the filter surface follows the input', () => {
  it('renders the chip bar, and nothing in the headers, when given a store', () => {
    const { fixture } = createWithDataTable(HostWithFilterStoreComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).not.toBeNull();
    expect(fixture.debugElement.queryAll(By.directive(ColumnFilter)).length).toBe(
      0,
    );
  });

  it('renders no filter surface at all when given none', () => {
    const { fixture } = createWithDataTable(HostWithoutActionsComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).toBeNull();
    expect(
      fixture.debugElement.queryAll(By.directive(ColumnFilter)).length,
    ).toBe(0);
  });
});

// The narrow job the directive has. Placing a filter ANYWHERE else — a drawer, a modal, a header
// cell — needs no directive: store.get(id) hands back the same handle and depends on nothing in
// the component tree, which is the whole reason the store belongs to the consumer.
describe('SpiderlyDataTableComponent — a projected filter control', () => {
  it('renders the consumer template for that filter, and the default for the rest', async () => {
    const { fixture } = createWithDataTable(HostWithFilterTemplateComponent);
    await renderRows(fixture);
    const el = fixture.nativeElement as HTMLElement;

    await openColumnMenu(fixture, 0);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-filter"]')!
      .click();
    await renderRows(fixture);

    const custom = el.querySelector<HTMLInputElement>(
      '[data-testid="custom-filter"]',
    );
    expect(custom).not.toBeNull();
    expect(el.querySelector('[data-testid="filter-editor-value"]')).toBeNull();

    // It drives the same store the built-in control does.
    custom!.value = 'Bosch';
    custom!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    await renderRows(fixture);

    expect(fixture.componentInstance.filters.applied()[0].value).toBe('Bosch');

    // The filter that projects nothing keeps the control the bar would have drawn.
    await openColumnMenu(fixture, 1);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-filter"]')!
      .click();
    await renderRows(fixture);

    expect(el.querySelector('[data-testid="filter-editor-value"]')).not.toBeNull();
  });
});


// "Clear filters" clears the FILTERS — the sort is not a filter (2026-09-06, grilled): Polaris
// wires onClearAll to the filters while sort is its own channel, and an operator clearing three
// chips would be surprised to lose the ordering too. One request, not two: the store's requery
// effect owns the refetch, and PrimeNG's table.clear() no longer fires the second, unsequenced
// one the 2026-09-05 review flagged.
describe('SpiderlyDataTableComponent — Clear filters clears only the filters', () => {
  it('keeps the sort, keeps its per-view override, and spends one request', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithViewSortComponent,
    );
    await renderRows(fixture);

    headerSort(dataTable, 'status'); // an operator override on "all"
    await fixture.ngZone!.run(async () => {
      host.filters.set('status', {
        operator: MatchModeCodes.Equals,
        value: 5,
      });
      host.filters.commit('status');
    });
    await renderRows(fixture);

    const before = host.captured.length;
    await fixture.ngZone!.run(async () => {
      (fixture.nativeElement as HTMLElement)
        .querySelector<HTMLButtonElement>('[data-testid="filter-bar-clear"]')!
        .click();
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before + 1);
    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.at(-1)!.multiSortMeta).toEqual([
      { field: 'status', order: 1 },
    ]);
    expect(sessionStorage.getItem(`${VIEW_SORT_STATE_KEY}:all:sort`))
      .withContext('clearing filters must not touch the sort override')
      .not.toBeNull();
  });
});

describe('SpiderlyDataTableComponent — a store on a client-side table fails loud', () => {
  // Armed anyway, the first commit would call the absent getPaginatedListObservableMethod
  // inside the requery effect and strand the pending overlay forever — a half-broken table
  // that looks fine until the first Apply. Client-side store predicates are unbuilt.
  it('throws at init instead of stranding the first commit', () => {
    expect(() => createFixture(HostClientSideWithStoreComponent)).toThrowError(
      /\[filters\] requires a lazy table/,
    );
  });
});
