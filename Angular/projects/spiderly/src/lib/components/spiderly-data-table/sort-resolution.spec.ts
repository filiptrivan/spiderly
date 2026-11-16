// Which sort applies (`SortResolution`), through the table: the declared default, and the sorts
// the backend cannot answer.

import { Component } from '@angular/core';
import { ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { Filter } from '../../entities/filter';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  capturingGetList,
  createFixture,
  createWithDataTable,
  DEFAULT_SORT_STATE_KEY,
  HostWithDefaultSortComponent,
  headerCells,
  HostWithHiddenDefaultSortFilterStoreComponent,
} from './spiderly-data-table.fixtures.spec';

function headerNamed(el: HTMLElement, name: string): HTMLTableCellElement {
  return headerCells(el).find((th) => (th.textContent ?? '').includes(name))!;
}

const STALE_SORT_STATE_KEY = 'sdt-stale-sort-spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [stateKey]="stateKey"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithPersistedCommaSeparatedSortComponent {
  cols: Column[] = [
    { name: 'Title', field: 'title', filterType: 'text' },
    { name: 'Sku', field: 'productVariantsCommaSeparated', filterType: 'text' },
  ];
  stateKey = STALE_SORT_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — declared default sort', () => {
  const ID_ASC = [{ field: 'id', order: 1 }];

  const setup = () => createWithDataTable(HostWithDefaultSortComponent);

  it('sends the declared default sort with the initial load', () => {
    const { host } = setup();

    expect(host.captured.length).toBe(1);
    expect(host.captured[0].multiSortMeta).toEqual(ID_ASC);
  });

  it('lets persisted state win over the declared default', () => {
    sessionStorage.setItem(
      DEFAULT_SORT_STATE_KEY,
      JSON.stringify({ multiSortMeta: [{ field: 'name', order: -1 }] }),
    );

    const { host } = setup();

    expect(host.captured[0].multiSortMeta).toEqual([
      { field: 'name', order: -1 },
    ]);
  });

  it('returns to the declared default when the tri-state header click un-sorts', () => {
    const { host, dataTable } = setup();
    const click = () =>
      dataTable.table.sort({
        originalEvent: new MouseEvent('click'),
        field: 'name',
      });

    click(); // name ascending
    click(); // name descending
    click(); // would be "unsorted" — must land on the default instead

    expect(host.captured[host.captured.length - 1].multiSortMeta).toEqual(ID_ASC);
    // Table state (header arrows, saved state) follows the same fallback.
    expect(dataTable.table._multiSortMeta).toEqual(ID_ASC);
  });

  // "Clear filters wipes the sort, so it must land on the default" died 2026-09-06: Clear no
  // longer touches sort at all (its own suite below), and on a storeless table like this one
  // the bar — Clear's only surface — never renders, so no gesture is left to pin here.
});

describe('SpiderlyDataTableComponent — no declared default sort', () => {
  it('keeps the initial load unsorted (backend applies its own Id fallback)', () => {
    const captured: Filter[] = [];

    @Component({
      imports: [SpiderlyDataTableComponent],
      template: `
        <spiderly-data-table
          [cols]="cols"
          [getPaginatedListObservableMethod]="getList"
        ></spiderly-data-table>
      `,
    })
    class HostWithoutDefaultSortComponent {
      cols = cols;
      getList = capturingGetList(captured);
    }

    createFixture(HostWithoutDefaultSortComponent);

    expect(captured.length).toBe(1);
    expect(captured[0].multiSortMeta ?? null).toBeNull();
  });
});

describe('SpiderlyDataTableComponent — CommaSeparated columns are not sortable', () => {
  // The backend's PaginatedResultGenerator never emits a sort case for *CommaSeparated collection
  // columns (the same naming convention decides both sides), and unknown sort fields are rejected
  // with a 400 — so the header must not offer the click. Before this rule, clicking such a header
  // (e.g. an admin SKU column) 500'd on the unknown sort field (2026-08-10, QueryableExtensionsApplySortTests).

  @Component({
    imports: [SpiderlyDataTableComponent],
    template: `
      <spiderly-data-table
        [cols]="cols"
        [getPaginatedListObservableMethod]="getList"
      ></spiderly-data-table>
    `,
  })
  class HostWithCommaSeparatedColumnComponent {
    cols: Column[] = [
      { name: 'Title', field: 'title', filterType: 'text' },
      { name: 'Sku', field: 'productVariantsCommaSeparated', filterType: 'text' },
      { name: 'Notes', field: 'notes', filterType: 'text', sortable: false },
    ];
    getList = emptyList;
  }

  const headerOf = (
    fixture: ComponentFixture<unknown>,
    name: string,
  ): HTMLTableCellElement => headerNamed(fixture.nativeElement, name);

  it('disables click-to-sort and hides the sort icon on a CommaSeparated field', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const skuHeader = headerOf(fixture, 'Sku');

    expect(skuHeader.classList.contains('p-datatable-sortable-column')).toBeFalse();
    expect(skuHeader.querySelector('p-sorticon')).toBeNull();
  });

  it('keeps ordinary columns sortable', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const titleHeader = headerOf(fixture, 'Title');

    expect(titleHeader.classList.contains('p-datatable-sortable-column')).toBeTrue();
    expect(titleHeader.querySelector('p-sorticon')).not.toBeNull();
  });

  it('still honors an explicit sortable: false', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const notesHeader = headerOf(fixture, 'Notes');

    expect(notesHeader.classList.contains('p-datatable-sortable-column')).toBeFalse();
    expect(notesHeader.querySelector('p-sorticon')).toBeNull();
  });
});

describe('SpiderlyDataTableComponent — persisted sort on a non-sortable column', () => {
  // Persisted state outlives the rule that produced it: a sort stored before the column became
  // non-sortable would ride every lazy load, and the backend answers an unknown sort field with a
  // 400 — unclearable from the UI, since the header that would clear it is no longer clickable.

  it('drops the stale sort instead of sending it', () => {
    sessionStorage.setItem(
      STALE_SORT_STATE_KEY,
      JSON.stringify({
        multiSortMeta: [{ field: 'productVariantsCommaSeparated', order: 1 }],
      }),
    );

    const fixture = createFixture(HostWithPersistedCommaSeparatedSortComponent);
    const host = fixture.componentInstance;

    expect(host.captured.length).toBe(1);
    expect(host.captured[0].multiSortMeta ?? null).toBeNull();
  });

  it('keeps a persisted sort on a sortable column', () => {
    sessionStorage.setItem(
      STALE_SORT_STATE_KEY,
      JSON.stringify({ multiSortMeta: [{ field: 'title', order: -1 }] }),
    );

    const fixture = createFixture(HostWithPersistedCommaSeparatedSortComponent);
    const host = fixture.componentInstance;

    expect(host.captured[0].multiSortMeta).toEqual([
      { field: 'title', order: -1 },
    ]);
  });
});

describe('SpiderlyDataTableComponent — a store table default-sorts by a hidden column', () => {
  // The legacy null-on-hidden exists so clear-on-hide is not undone by the default sneaking back
  // in — an invisible ordering. On a store table the sort chip names the default like any other
  // sort, so a hidden column is as good a default as a visible one.
  it('applies the declared default sort even while its column is hidden', () => {
    const fixture = createFixture(HostWithHiddenDefaultSortFilterStoreComponent);
    const host = fixture.componentInstance;

    expect(host.captured[0].multiSortMeta).toEqual([
      { field: 'name', order: 1 },
    ]);
  });
});
