import { Component } from '@angular/core';
import { Subject } from 'rxjs';
import { LazyLoadSelectedIdsResult } from '../../entities/lazy-load-selected-ids-result';
import { PaginatedResult } from '../../entities/paginated-result';
import { renderRows } from '../../testing/spec-support.spec';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { cols, createWithDataTable } from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [getPaginatedListObservableMethod]="getList"
      [selectedLazyLoadObservableMethod]="getSelected"
    ></spiderly-data-table>
  `,
})
class HostWithControlledLoadsComponent {
  cols = cols;
  pages: Subject<PaginatedResult>[] = [];
  selections: Subject<LazyLoadSelectedIdsResult>[] = [];

  getList = () => {
    const page = new Subject<PaginatedResult>();
    this.pages.push(page);
    return page;
  };

  getSelected = () => {
    const selection = new Subject<LazyLoadSelectedIdsResult>();
    this.selections.push(selection);
    return selection;
  };
}

const emptySelection = new LazyLoadSelectedIdsResult({ selectedIds: [], totalRecordsSelected: 0 });

describe('SpiderlyDataTableComponent — overlapping loads', () => {
  it('keeps the latest rows and total when an older response arrives last', async () => {
    const { fixture, host, dataTable } = createWithDataTable(HostWithControlledLoadsComponent);
    fixture.ngZone!.run(() => dataTable.reload());
    expect(host.pages.length).toBe(2);
    const totals = jasmine.createSpy('total records');
    dataTable.onTotalRecordsChange.subscribe(totals);

    host.selections[1].next(emptySelection);
    host.pages[1].next({ data: [{ id: 2 }], totalRecords: 20 });
    await renderRows(fixture);
    expect(dataTable.items).toEqual([{ id: 2 }]);

    host.selections[0].next(emptySelection);
    host.pages[0].next({ data: [{ id: 1 }], totalRecords: 10 });
    await renderRows(fixture);

    expect(dataTable.items).toEqual([{ id: 2 }]);
    expect(dataTable.totalRecords).toBe(20);
    expect(totals).toHaveBeenCalledOnceWith(20);
  });

  it('keeps the latest load pending when an older request fails', async () => {
    const { fixture, host, dataTable } = createWithDataTable(HostWithControlledLoadsComponent);
    fixture.ngZone!.run(() => dataTable.reload());

    host.selections[0].next(emptySelection);
    host.pages[0].error(new Error('older page failed'));
    expect(dataTable.isPending).toBeTrue();

    host.selections[1].next(emptySelection);
    host.pages[1].next({ data: [{ id: 2 }], totalRecords: 20 });
    await renderRows(fixture);
    expect(dataTable.isPending).toBeFalse();
  });

  it('waits for the latest page selection when older selected ids finish first', async () => {
    const { fixture, host, dataTable } = createWithDataTable(HostWithControlledLoadsComponent);
    fixture.ngZone!.run(() => {
      host.pages[0].next({ data: [{ id: 1 }], totalRecords: 10 });
      dataTable.reload();
      host.pages[1].next({ data: [{ id: 2 }], totalRecords: 20 });
      host.selections[0].next(new LazyLoadSelectedIdsResult({ selectedIds: [1], totalRecordsSelected: 7 }));
    });
    await renderRows(fixture);
    expect(dataTable.rowsSelectedNumber).toBe(0);
    expect(dataTable.isPending).toBeTrue();

    fixture.ngZone!.run(() => {
      host.selections[1].next(new LazyLoadSelectedIdsResult({ selectedIds: [2], totalRecordsSelected: 1 }));
    });
    await renderRows(fixture);
    expect(dataTable.rowsSelectedNumber).toBe(1);
    expect(dataTable.isPending).toBeFalse();
  });
});
