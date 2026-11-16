// A view's filter payload (`ActiveView.viewFilterPayload`), through the table.

import { Component, computed } from '@angular/core';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Filter } from '../../entities/filter';
import { booleanFilter, createFilterStore, numberFilter } from '../../filters/filter-store';
import { Column, SpiderlyDataTableComponent, TableView } from './spiderly-data-table.component';
import {
  cols,
  capturingGetList,
  createWithDataTable,
  remount,
  clickView,
  packingView,
  HostWithTransientViewComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-view-payload-spec'"
      [stateStorage]="'local'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithViewFilterPayloadsComponent {
  filters = createFilterStore({
    status: numberFilter({ label: 'Status' }),
    isCompanyOrder: booleanFilter({ label: 'Porudžbina firme' }),
  });
  cols: Column[] = [{ name: 'Naziv', field: 'name', filterType: 'text' }];
  views: TableView[] = [{ id: 'all', label: 'Sve' }, packingView];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

const companyOrdersOnly = (filters: HostWithViewFilterPayloadsComponent['filters']) =>
  filters.setAndCommit('isCompanyOrder', {
    operator: MatchModeCodes.Equals,
    value: true,
  });

// A view's count has to be the number of rows a click on its tab lists, and an operator's bar
// filters saved on a view narrow that list (spiderly#439: one operator keeps "company orders only"
// on two tabs). So the consumer's count query needs each view's filters as the request payload,
// read without entering the view.
describe('SpiderlyDataTableComponent — a view\'s filter payload', () => {
  it('reads the filters saved on a view the operator left, without entering it or asking the server', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithViewFilterPayloadsComponent,
    );
    await renderRows(fixture);
    await clickView(fixture, 1);
    await fixture.ngZone!.run(async () => companyOrdersOnly(host.filters));
    await renderRows(fixture);
    await clickView(fixture, 0);
    const requestsBefore = host.captured.length;

    const payload = dataTable.viewFilterPayload('packing');
    await renderRows(fixture);

    expect(payload).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
      isCompanyOrder: [{ matchMode: MatchModeCodes.Equals, value: true }],
    } as any);
    expect(dataTable.activeViewId).toBe('all');
    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.length).toBe(requestsBefore);

    await clickView(fixture, 1);

    expect(host.captured.at(-1)!.filters)
      .withContext('the payload is what the view\'s own list sends')
      .toEqual(payload!);
  });

  // The operator who saved a filter on a tab yesterday expects today's count to honor it.
  it('reads the filters saved on a view across a reload', async () => {
    const first = createWithDataTable(HostWithViewFilterPayloadsComponent);
    await renderRows(first.fixture);
    await clickView(first.fixture, 1);
    await first.fixture.ngZone!.run(async () => companyOrdersOnly(first.host.filters));
    await renderRows(first.fixture);
    await clickView(first.fixture, 0);

    const next = await remount(first.fixture, HostWithViewFilterPayloadsComponent);

    expect(next.dataTable.activeViewId).toBe('all');
    expect(next.dataTable.viewFilterPayload('packing')).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
      isCompanyOrder: [{ matchMode: MatchModeCodes.Equals, value: true }],
    } as any);
  });

  // Nothing stored is what a click answers with the view's own question — whether the operator
  // never opened it or cleared it — so the payload asks it too, on a store of its own: the bar the
  // operator is looking at must not change.
  it('asks a view\'s own question when nothing is saved on it, leaving the live filters alone', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithViewFilterPayloadsComponent,
    );
    await renderRows(fixture);
    await fixture.ngZone!.run(async () => companyOrdersOnly(host.filters));
    await renderRows(fixture);
    const requestsBefore = host.captured.length;

    const payload = dataTable.viewFilterPayload('packing');
    await renderRows(fixture);

    expect(payload).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
    expect(host.filters.applied().map((chip) => chip.id)).toEqual([
      'isCompanyOrder',
    ]);
    expect(host.captured.length).toBe(requestsBefore);
  });

  // The active view's count is the list on screen, so its payload is the live bar — including a
  // bar the operator cleared, where a click would re-ask the view's question but the list has not.
  it('reads the active view off the live filters', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithViewFilterPayloadsComponent,
    );
    await renderRows(fixture);
    await clickView(fixture, 1);
    await fixture.ngZone!.run(async () => host.filters.clear());
    await renderRows(fixture);

    expect(dataTable.viewFilterPayload('packing')).toEqual({});
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });

  it('re-derives a transient view\'s question instead of reading what an earlier visit saved', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithTransientViewComponent,
    );
    await renderRows(fixture);
    await clickView(fixture, 1);
    await clickView(fixture, 0);

    expect(dataTable.viewFilterPayload('today')).toEqual({
      createdAt: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
    expect(host.applyCount).toBe(2);
  });

  // A signals-first page derives what it sends from `computed`s, and Angular refuses a signal
  // write inside one — the scratch store's writes are nobody's business but the read's own.
  it('can be read inside a computed', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithViewFilterPayloadsComponent,
    );
    await renderRows(fixture);

    const payload = computed(() => dataTable.viewFilterPayload('packing'));

    expect(payload()).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
  });

  it('answers null for a view the table does not hold', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithViewFilterPayloadsComponent,
    );
    await renderRows(fixture);

    expect(dataTable.viewFilterPayload('ghost')).toBeNull();
  });

  it('answers an empty payload on a table without a filter store, as its list sends', async () => {
    @Component({
      imports: [SpiderlyDataTableComponent],
      template: `
        <spiderly-data-table
          [cols]="cols"
          [views]="views"
          [getPaginatedListObservableMethod]="getList"
        ></spiderly-data-table>
      `,
    })
    class HostWithStorelessViewsComponent {
      cols = cols;
      views: TableView[] = [{ id: 'all', label: 'Sve' }, packingView];
      captured: Filter[] = [];
      getList = capturingGetList(this.captured);
    }

    const { fixture, host, dataTable } = createWithDataTable(
      HostWithStorelessViewsComponent,
    );
    await renderRows(fixture);
    await clickView(fixture, 1);

    expect(dataTable.viewFilterPayload('packing')).toEqual({});
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });
});
