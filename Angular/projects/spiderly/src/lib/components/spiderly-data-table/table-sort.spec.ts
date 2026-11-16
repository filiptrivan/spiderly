// The sort on the grid (`TableSort`), through the table: the bar's picker and its labels, and a
// hidden column's sort.

import { Component } from '@angular/core';
import { ComponentFixture } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { Filter } from '../../entities/filter';
import { createFilterStore, numberFilter, textFilter } from '../../filters/filter-store';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { Column, SpiderlyDataTableComponent, TableView } from './spiderly-data-table.component';
import {
  cols,
  capturingGetList,
  createWithDataTable,
  remount,
  openChooser,
  clickOption,
  HostWithCapturingFilterStoreComponent,
  VIEW_SORT_STATE_KEY,
  HostWithViewSortComponent,
  HostWithHiddenDefaultSortFilterStoreComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-columnless-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithColumnlessViewSortComponent {
  cols: Column[] = [{ name: 'Id', field: 'id', filterType: 'numeric' }];
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  views: TableView[] = [
    {
      id: 'newest',
      label: 'Najnovije',
      sort: [{ field: 'createdAt', order: -1 }],
    },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="'sdt-store-hide-keeps-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSortableFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text' },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

// ---------------------------------------------------------------------------
// STORELESS tables (the generated M2M details grids, until spiderly#407 scaffolds their stores)
// have no bar — so nothing on screen can name a hidden column's sort. For them alone, the old
// "hidden contributes nothing" rule survives on the SORT axis: hiding a sorted column drops its
// sort, and a hidden defaultSortField column does not apply. Store tables keep decision 2b.
// ---------------------------------------------------------------------------

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [stateKey]="'sdt-storeless-hidden-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostStorelessSortableComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [defaultSortField]="'id'"
      [stateKey]="'sdt-storeless-hidden-default-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostStorelessDefaultSortComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

// The bar's picker drives the same contract as the header menu: ONE sort, replacing the whole
// meta, persisted as the active view's override; reset lands on the resolved default (view
// declared ?? table default ?? unsorted) and empties the override slot, so the declared
// ordering flows through again. The options are built from ALL sortable columns — hidden ones
// included, which is the capability the picker exists for: a hidden column has no header.
describe('SpiderlyDataTableComponent — sorting from the bar', () => {
  const barOf = (fixture: ComponentFixture<unknown>) =>
    fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent))
      .componentInstance as SpiderlyFilterBarComponent;

  const pickFromBar = async (
    fixture: ComponentFixture<unknown>,
    field: string,
    order: 1 | -1,
  ) => {
    await fixture.ngZone!.run(async () =>
      barOf(fixture).sortPick.emit({ field, order }),
    );
    await renderRows(fixture);
  };

  it('a pick sorts the grid and persists as the view override', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewSortComponent);
    await renderRows(fixture);

    await pickFromBar(fixture, 'status', -1);

    expect(host.captured.at(-1)!.multiSortMeta).toEqual([
      { field: 'status', order: -1 },
    ]);
    expect(
      sessionStorage.getItem(`${VIEW_SORT_STATE_KEY}:all:sort`),
    ).not.toBeNull();
  });

  it('reset lands on the resolved default and empties the override slot', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewSortComponent);
    await renderRows(fixture);
    await pickFromBar(fixture, 'status', -1);

    await fixture.ngZone!.run(async () => barOf(fixture).sortReset.emit());
    await renderRows(fixture);

    expect(host.captured.at(-1)!.multiSortMeta).toEqual([
      { field: 'id', order: -1 },
    ]);
    expect(
      sessionStorage.getItem(`${VIEW_SORT_STATE_KEY}:all:sort`),
    ).toBeNull();
  });

  // Re-sorting re-orders the WHOLE result set — page 7 of the new ordering is a continuation
  // nobody asked for, and the header click already resets (PrimeNG's resetPageOnSort). Every
  // applySort gesture pages like the header.
  it('a pick lands on page one, like a header sort', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithViewSortComponent,
    );
    await renderRows(fixture);
    dataTable.table.first = 25;

    await pickFromBar(fixture, 'status', -1);

    expect(host.captured.at(-1)!.first).toBe(0);
  });

  // sortMultiple() never saves table state (PrimeNG's sort() does), so on a VIEWLESS store
  // table — where the PrimeNG blob is the ONLY sort memory — a menu/bar sort evaporated on F5
  // while the identical header click survived. applySort saves like the header path.
  it('a bar sort on a viewless table survives a reload', async () => {
    const first = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(first.fixture);

    await pickFromBar(first.fixture, 'id', -1);

    const { host } = await remount(
      first.fixture,
      HostWithCapturingFilterStoreComponent,
    );

    expect(host.captured[0].multiSortMeta).toEqual([{ field: 'id', order: -1 }]);
  });

  it('offers hidden sortable columns to the picker', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithHiddenDefaultSortFilterStoreComponent,
    );
    await renderRows(fixture);

    expect(dataTable.sortPickOptions).toEqual([
      { field: 'id', label: 'Id' },
      { field: 'name', label: 'Naziv' },
    ]);
  });
});

// Decision 2b's sort half taken literally: an ordering does not need its column — but its chip
// must not print the raw camelCase identifier at an operator. The label resolves through the
// scaffold's PascalCase i18n-key convention instead (Transloco falls back to the key itself, so
// this pins the ROUTE — 'CreatedAt', never 'createdAt' — not the final word).
describe('SpiderlyDataTableComponent — a column-less sort key still gets a spoken label', () => {
  it('translates the field instead of printing it raw', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithColumnlessViewSortComponent,
    );
    await renderRows(fixture);

    expect(dataTable.sortKeys[0]).toEqual({
      field: 'createdAt',
      label: 'CreatedAt',
      descending: true,
    });
  });
});

// Decision 2b's other half: the whole "hidden contributes nothing" apparatus exists because the
// HEADER was the only place a constraint could be seen. On a store table the chip bar names the
// filters and the sort chip names the sort, so hiding a column no longer silently hides what the
// grid is narrowed or ordered by — and dropping the constraint on hide becomes the destructive
// act, not the safe one.
describe('SpiderlyDataTableComponent — a store table hides columns without touching constraints', () => {
  it('hiding a sorted column keeps the sort: the sort chip names it now', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithSortableFilterStoreComponent,
    );
    await renderRows(fixture);

    await fixture.ngZone!.run(async () =>
      dataTable.table.sort({
        originalEvent: new MouseEvent('click'),
        field: 'name',
      }),
    );
    await renderRows(fixture);
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Naziv');
    await renderRows(fixture);

    expect(host.captured.length)
      .withContext('a hide is a layout change, not a query change')
      .toBe(loadsBefore);

    await fixture.ngZone!.run(async () => dataTable.reload());
    await renderRows(fixture);

    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some(
        (meta) => meta.field === 'name',
      ),
    )
      .withContext('the hidden column must keep sorting')
      .toBeTrue();
  });
});

describe('SpiderlyDataTableComponent — a storeless table drops a hidden column\'s sort', () => {
  it('hiding a sorted column removes its sort and reloads exactly once, without it', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostStorelessSortableComponent,
    );

    dataTable.table.sort({
      originalEvent: new MouseEvent('click'),
      field: 'name',
    });
    fixture.detectChanges();
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(host.captured.length)
      .withContext('one reload after hiding a sorted column')
      .toBe(loadsBefore + 1);
    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some((m) => m.field === 'name'),
    )
      .withContext('a hidden column must not sort a table with no surface to name it')
      .toBeFalse();
  });

  it('does not reload when hiding an unsorted column', async () => {
    const { fixture, host } = createWithDataTable(HostStorelessSortableComponent);
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(host.captured.length).toBe(loadsBefore);
  });

  it('leaves the reload unsorted instead of re-applying an invisible default sort', async () => {
    const { fixture, host } = createWithDataTable(HostStorelessDefaultSortComponent);
    expect(host.captured[0].multiSortMeta).toEqual([{ field: 'id', order: 1 }]);

    await openChooser(fixture);
    clickOption(fixture, 'Id');

    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some((m) => m.field === 'id'),
    )
      .withContext('hidden default-sort column must not sort a storeless table')
      .toBeFalse();
  });
});
