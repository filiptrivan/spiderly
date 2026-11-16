// The active view and its filters (`ActiveView`), through the table: transient views, a reload,
// the tab counts, and the filters a reload keeps.

import { Component } from '@angular/core';
import { ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Filter } from '../../entities/filter';
import { createFilterStore, numberFilter } from '../../filters/filter-store';
import { Column, SpiderlyDataTableComponent, TableView } from './spiderly-data-table.component';
import {
  cols,
  capturingGetList,
  createWithDataTable,
  remount,
  viewTabs,
  clickView,
  HostWithCapturingFilterStoreComponent,
  packingView,
  HostWithViewsComponent,
  HostWithTransientViewComponent,
  HostWithLateViewsComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-view-counts-spec'"
      [stateStorage]="'local'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithViewCountsComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [{ name: 'Naziv', field: 'name', filterType: 'text' }];
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    { ...packingView, count: 12 },
    { id: 'companies', label: 'Firme', count: 0 },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);

  // What a consumer does after each list load: the same views, rebuilt with fresh numbers.
  refreshCounts(counts: Record<string, number | null>): void {
    this.views = this.views.map((view) => ({ ...view, count: counts[view.id] }));
  }
}

async function refreshCountsAndRender(
  mounted: {
    fixture: ComponentFixture<HostWithViewCountsComponent>;
    host: HostWithViewCountsComponent;
  },
  counts: Record<string, number | null>,
): Promise<void> {
  await mounted.fixture.ngZone!.run(async () => {
    mounted.host.refreshCounts(counts);
    mounted.fixture.detectChanges();
  });
  await renderRows(mounted.fixture);
}

const viewCounts = (fixture: ComponentFixture<unknown>): (string | null)[] =>
  Array.from(viewTabs(fixture)).map(
    (tab) =>
      tab
        .querySelector('[data-testid="table-view-count"]')
        ?.textContent?.trim() ?? null,
  );

// A view whose apply is a function of NOW cannot live under stored-wins-over-apply: "Danas
// primljene" clicked yesterday stores yesterday's midnight, and today's click would restore it —
// a tab named "today" showing yesterday, with only the chip's date to give it away. A transient
// view re-derives instead of remembering.
describe('SpiderlyDataTableComponent — transient views', () => {
  it('re-applies on every select instead of restoring what an earlier visit stored', async () => {
    const { fixture, host } = createWithDataTable(HostWithTransientViewComponent);
    await renderRows(fixture);

    await clickView(fixture, 1);
    expect(host.filters.applied()[0].value).toBe(1);

    await clickView(fixture, 0);
    await clickView(fixture, 1);

    expect(host.filters.applied()[0].value)
      .withContext('the second visit must re-derive, not remember')
      .toBe(2);
  });
});

// Every persisted key carries the view segment, but the segment's SOURCE — activeViewId — lived
// only in memory: an F5 seeded it back to the first view and the restore read `…:all:filters`,
// so the tab snapped to "Sve" and the bar came back empty while the operator's filters sat
// intact under `…:packing:filters` (Filip, on /porudzbine). The id persists now, beside the
// filters it scopes.
describe('SpiderlyDataTableComponent — the active view survives a reload', () => {
  async function mountWithStoredPacking() {
    sessionStorage.setItem('sdt-late-views-spec:view', JSON.stringify('packing'));
    const mounted = createWithDataTable(HostWithLateViewsComponent);
    await renderRows(mounted.fixture);
    return mounted;
  }

  async function revealLateView(mounted: {
    fixture: ComponentFixture<HostWithLateViewsComponent>;
    host: HostWithLateViewsComponent;
  }): Promise<void> {
    await mounted.fixture.ngZone!.run(async () => {
      mounted.host.revealPacking();
      mounted.fixture.detectChanges();
    });
    await renderRows(mounted.fixture);
  }

  it('lands on the stored view with its filters in the first request', async () => {
    const first = createWithDataTable(HostWithViewsComponent);
    await renderRows(first.fixture);
    await clickView(first.fixture, 1);

    const next = await remount(first.fixture, HostWithViewsComponent);

    expect(next.dataTable.activeViewId).toBe('packing');
    expect(
      viewTabs(next.fixture)[1].classList.contains('table-view-active'),
    ).toBeTrue();
    expect(next.host.filters.applied().map((chip) => chip.id)).toEqual([
      'status',
    ]);
    expect(next.host.captured[0].filters).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
  });

  it('falls back to the first view when the stored one does not exist, keeping the key', async () => {
    sessionStorage.setItem('spiderly-table:/:view', JSON.stringify('ghost'));

    const { fixture, dataTable } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    expect(dataTable.activeViewId).toBe('all');
    expect(
      viewTabs(fixture)[0].classList.contains('table-view-active'),
    ).toBeTrue();
    expect(sessionStorage.getItem('spiderly-table:/:view'))
      .withContext('"vanished" and "not yet arrived" look the same — keep the key')
      .toBe(JSON.stringify('ghost'));
  });

  it('restores a transient view\'s tab but re-derives its question', async () => {
    // Storage deliberately holds a DIFFERENT answer (2) than a fresh apply derives (1) — the
    // discriminator between re-deriving and restoring. The click→persist path that writes these
    // keys is already pinned by the transient-views suite and the spec above.
    sessionStorage.setItem('sdt-transient-view-spec:view', JSON.stringify('today'));
    sessionStorage.setItem(
      'sdt-transient-view-spec:today:filters',
      JSON.stringify({ createdAt: { operator: MatchModeCodes.Equals, value: 2 } }),
    );

    const { fixture, host, dataTable } = createWithDataTable(
      HostWithTransientViewComponent,
    );
    await renderRows(fixture);

    expect(dataTable.activeViewId).toBe('today');
    expect(host.filters.applied()[0].value)
      .withContext('a reload must re-derive, not restore yesterday\'s answer')
      .toBe(1);
  });

  it('adopts a stored view that only arrives on a later [views] change', async () => {
    const mounted = await mountWithStoredPacking();

    expect(mounted.dataTable.activeViewId)
      .withContext('until the view exists, the seed stands')
      .toBe('all');

    await revealLateView(mounted);

    expect(mounted.dataTable.activeViewId).toBe('packing');
    expect(mounted.host.filters.applied().map((chip) => chip.id)).toEqual([
      'status',
    ]);
    expect(mounted.host.captured.at(-1)!.filters).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
  });

  it('lets an explicit pick beat a stored view that arrives later', async () => {
    const mounted = await mountWithStoredPacking();

    await clickView(mounted.fixture, 1);
    await revealLateView(mounted);

    expect(mounted.dataTable.activeViewId)
      .withContext('the operator picked companies since the reload — that wins')
      .toBe('companies');
  });
});

// An operator should see which work list has something in it without clicking through every tab
// (spiderly#430). Polaris IndexFilters carries the same badge on its tabs.
describe('SpiderlyDataTableComponent — a view tab shows its count', () => {
  it('shows the count beside the label, zero included, and nothing for a view without one', async () => {
    const { fixture } = createWithDataTable(HostWithViewCountsComponent);
    await renderRows(fixture);

    expect(viewCounts(fixture)).toEqual([null, '12', '0']);
    expect(viewTabs(fixture)[1].textContent).toContain('Za pakovanje');
  });

  // Counts refresh on every list load, so a refresh that re-entered a view would be a view switch
  // the operator never asked for. The case that did it: with `stateStorage: 'local'` a pick in a
  // second browser tab rewrites the stored view id, and the next rebuild of `[views]` here adopted
  // it — swapping this tab's rows under the operator.
  it('updates the counts without moving the operator or asking the server again', async () => {
    const mounted = createWithDataTable(HostWithViewCountsComponent);
    await renderRows(mounted.fixture);
    const requestsBefore = mounted.host.captured.length;

    localStorage.setItem('sdt-view-counts-spec:view', JSON.stringify('packing'));
    await refreshCountsAndRender(mounted, { all: null, packing: 13, companies: 1 });

    expect(viewCounts(mounted.fixture)).toEqual([null, '13', '1']);
    expect(mounted.dataTable.activeViewId).toBe('all');
    expect(mounted.host.filters.applied()).toEqual([]);
    expect(mounted.host.captured.length).toBe(requestsBefore);
  });

  // A rebuilt `[views]` holds new objects every time; re-rendering the tabs for them would drop
  // keyboard focus from the tab the operator is on, once per list load.
  it('keeps a focused tab focused through a count refresh', async () => {
    const mounted = createWithDataTable(HostWithViewCountsComponent);
    await renderRows(mounted.fixture);

    viewTabs(mounted.fixture)[1].focus();
    await refreshCountsAndRender(mounted, { packing: 13 });

    expect(document.activeElement).toBe(viewTabs(mounted.fixture)[1]);
  });
});


// The header filters were persisted for free by PrimeNG's stateful table; the bar owns them now,
// so nothing was writing them and a refresh dropped every one (Filip, on /tags). This is the
// regression that shipped with the bar, not a new capability.
describe('SpiderlyDataTableComponent — applied filters survive a reload', () => {
  it('carries them into the first request of the next mount', async () => {
    const first = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(first.fixture);

    await first.fixture.ngZone!.run(async () => {
      first.host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      first.host.filters.commit('companyName');
    });
    await renderRows(first.fixture);

    const next = await remount(
      first.fixture,
      HostWithCapturingFilterStoreComponent,
    );

    expect(next.host.filters.applied().map((chip) => chip.id)).toEqual([
      'companyName',
    ]);
    expect(next.host.captured[0].filters).toEqual({
      companyName: [
        { matchMode: MatchModeCodes.Contains, value: 'Elektromont' },
      ],
    } as any);
  });

  it('forgets them when the bar is cleared', async () => {
    const first = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(first.fixture);

    await first.fixture.ngZone!.run(async () => {
      first.host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      first.host.filters.commit('companyName');
      first.host.filters.clear();
    });
    await renderRows(first.fixture);

    const next = await remount(
      first.fixture,
      HostWithCapturingFilterStoreComponent,
    );

    expect(next.host.filters.applied()).toEqual([]);
  });
});
