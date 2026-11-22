// A committed filter re-queries (`PageLoader.requeryOnAppliedFilters`), through the table.

import { Component, signal } from '@angular/core';
import { Observable, of } from 'rxjs';
import { paginated, renderRows } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Filter } from '../../entities/filter';
import { PaginatedResult } from '../../entities/paginated-result';
import { createFilterStore, textFilter } from '../../filters/filter-store';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  capturingGetList,
  createWithDataTable,
  HostWithCapturingFilterStoreComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [defaultSortField]="'name'"
      [defaultSortOrder]="-1"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSortedFilterStoreComponent {
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Id', field: 'id', filterType: 'numeric' },
  ];
  getList = emptyList;
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
}

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
class HostWithCountingFilterStoreComponent {
  cols = cols;
  getList = (): Observable<PaginatedResult> => paginated([{ id: 1 }, { id: 2 }], 812);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

const REQUERY_PERSIST_STATE_KEY = 'sdt-requery-persist-spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="stateKey"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithPersistedFilterStoreComponent {
  cols = cols;
  stateKey = REQUERY_PERSIST_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — a committed filter re-queries', () => {
  it('sends the store payload and asks the server again, from page one', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before + 1);
    expect(host.captured.at(-1)!.filters).toEqual({
      companyName: [
        { matchMode: MatchModeCodes.Contains, value: 'Elektromont' },
      ],
    } as any);
    // Narrowing from page 3 must not keep asking the server to skip rows a smaller result set
    // does not have — the failure is an empty grid for a value that is definitely there.
    expect(host.captured.at(-1)!.first).toBe(0);
  });

  it('does not re-query when a signal read by the list callback changes', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    const teamId = signal(1);
    const getList = host.getList;
    host.getList = (filter) =>
      getList({ ...filter, additionalFilterIdLong: teamId() });
    await renderRows(fixture);

    const before = host.captured.length;
    await fixture.ngZone!.run(async () => {
      host.filters.setAndCommit('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before + 1);
    expect(host.captured.at(-1)?.additionalFilterIdLong).toBe(1);

    await fixture.ngZone!.run(async () => teamId.set(2));
    await renderRows(fixture);

    expect(host.captured.length).toBe(before + 1);
  });

  // A draft is not a query: re-fetching on every keystroke is the other half of the lie the chip
  // bar exists to prevent, paid for in requests instead of in credibility.
  it('does not re-query for a draft', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elek',
      });
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before);
  });

  // A stray x on a filter nobody applied, or a commit of an unchanged draft, must not spend a
  // request. The effect watches `applied()`, and a freshly built Map is a new identity even when
  // its contents are the same, so without a guard every such gesture re-queries.
  it('does not re-query when a commit or reset changes nothing', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.commit('companyName');
      host.filters.reset('companyName');
      host.filters.clear();
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before);
  });

  // An empty grid under a filter is otherwise a mystery: the chips say what was asked, and this
  // says what came back. It is the CURRENT query's count only — the unfiltered total would cost a
  // second request the backend does not offer.
  it('shows how many rows the current query returned', async () => {
    const { fixture } = createWithDataTable(HostWithCountingFilterStoreComponent);
    await renderRows(fixture);

    expect(
      (fixture.nativeElement as HTMLElement).querySelector(
        '[data-testid="filter-bar-count"]',
      )!.textContent,
    ).toContain('812');
  });

  // Multi-sort is invisible today: the only sign is sort icons scattered across headers, and
  // nothing says which of them is the primary key. The chip answers "why is the list in this
  // order" in one place, using the COLUMN NAME rather than the field an operator never sees.
  it('shows what the grid is sorted by, by name and direction', async () => {
    const { fixture } = createWithDataTable(HostWithSortedFilterStoreComponent);
    await renderRows(fixture);

    const sortChip = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="sort-chip"]',
    );

    expect(sortChip).not.toBeNull();
    expect(sortChip!.textContent).toContain('Naziv');
    expect(sortChip!.textContent).toContain('↓');
    expect(sortChip!.textContent).not.toContain('name');
  });

  // Two buttons for one job is the complaint. With a store supplied the toolbar's "Clear filters"
  // sits a row away from the chips it clears, so it goes and the bar owns the gesture. (What the
  // gesture reaches narrowed 2026-09-06 — filters only; the "clears only the filters" suite pins
  // that side.)
  it('clears from the bar, and the toolbar no longer offers a second way', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    const el = fixture.nativeElement as HTMLElement;
    // Scoped to the TOOLBAR: the bar's own clear button carries the same icon, deliberately (one
    // meaning, one glyph), so a bare icon query stopped meaning "the toolbar button is gone".
    expect(el.querySelector('.table-header .pi-filter-slash')).toBeNull();

    await fixture.ngZone!.run(async () => {
      el.querySelector<HTMLButtonElement>(
        '[data-testid="filter-bar-clear"]',
      )!.click();
    });
    await renderRows(fixture);

    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });

  // A bare digit beside a chip reads as a badge on it. The number needs a word.
  it('labels the result count instead of printing a bare number', async () => {
    const { fixture } = createWithDataTable(HostWithCountingFilterStoreComponent);
    await renderRows(fixture);

    const count = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="filter-bar-count"]',
    )!;

    expect(count.textContent!.trim()).not.toBe('812');
    expect(count.textContent).toContain('812');
  });

});

describe('SpiderlyDataTableComponent — a committed filter re-persists page one', () => {
  // The commit path resets table.first to 0 but PrimeNG saves state only from its own
  // interactions — so without an explicit re-persist, the pre-commit page offset survives in
  // storage and the NEXT visit replays it against the narrowed result set: the backend answers
  // data=[] for an offset the filtered total no longer reaches, and the grid renders empty
  // under chips claiming matches.
  it('overwrites a persisted deep page offset with 0 at commit', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithPersistedFilterStoreComponent,
    );
    await renderRows(fixture);

    fixture.ngZone!.run(() => {
      dataTable.table.first = 100;
      dataTable.table.saveState();
    });

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    const persisted = JSON.parse(
      sessionStorage.getItem(REQUERY_PERSIST_STATE_KEY) ?? '{}',
    );
    expect(persisted.first)
      .withContext('the persisted offset must follow the commit back to page one')
      .toBe(0);
  });
});
