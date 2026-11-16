// The operator's column layout (`ColumnLayout`), through the table: the chooser, its guards, the
// reset, and the layout each view keeps.

import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { createFilterStore, numberFilter, textFilter } from '../../filters/filter-store';
import { Column, SpiderlyDataTableComponent, TableView } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  createFixture,
  createWithDataTable,
  remount,
  clickView,
  headerCells,
  openChooser,
  columnMenu,
  chooserContainer,
  chooserOptions,
  clickOption,
  HostWithLockedColumnComponent,
  HostWithTwoColumnsComponent,
  openColumnMenu,
  HostWithViewsComponent,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithHiddenColumnComponent {
  cols = idNameStockCols;
  getList = emptyList;
}

const COLUMNS_STATE_KEY = 'sdt-columns-spec';

// Id / Name visible, Stock hidden by declaration, plus an actions column.
const idNameStockCols: Column[] = [
  { name: 'Id', field: 'id', filterType: 'numeric' },
  { name: 'Name', field: 'name', filterType: 'text' },
  { name: 'Stock', field: 'stock', filterType: 'numeric', visible: false },
  { actions: [{ name: 'Details', field: 'Details' }] },
];

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
class HostWithColumnsStateKeyComponent {
  cols = idNameStockCols;
  stateKey = COLUMNS_STATE_KEY;
  getList = emptyList;
}

function headerTexts(el: HTMLElement): string[] {
  return headerCells(el).map((th) => th.textContent?.trim() ?? '');
}

function chooserReset(
  fixture: ComponentFixture<unknown>,
): HTMLButtonElement | null {
  return (
    chooserContainer(fixture)?.querySelector<HTMLButtonElement>(
      '[data-testid="column-chooser-reset"]',
    ) ?? null
  );
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-view-columns-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithViewColumnsComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text', lockVisible: true },
    { name: 'Lokacija', field: 'location', filterType: 'text' },
    { name: 'Zašto', field: 'why', filterType: 'text', visible: false },
  ];
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    { id: 'late', label: 'Kasni roba', columns: { why: true, location: false } },
  ];
  getList = emptyList;
}

const shownHeaders = (fixture: ComponentFixture<unknown>): string[] =>
  headerTexts(fixture.nativeElement).filter((text) =>
    ['Naziv', 'Lokacija', 'Zašto'].includes(text),
  );

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="'sdt-store-no-phantom-reveal-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithHiddenColumnFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text', visible: false },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  getList = emptyList;
}

describe('SpiderlyDataTableComponent — column visibility', () => {
  it('does not render a column declared visible: false', () => {
    const el: HTMLElement = createFixture(
      HostWithHiddenColumnComponent,
    ).nativeElement;

    const headers = headerTexts(el);
    expect(headers.some((h) => h.includes('Name'))).toBeTrue();
    expect(headers.some((h) => h.includes('Stock'))).toBeFalse();
  });

  it('lists only data columns in the chooser, hidden ones unchecked', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);

    const options = chooserOptions(fixture);
    expect(options.map((o) => o.label)).toEqual(['Id', 'Name', 'Stock']);
    expect(options.map((o) => o.input.checked)).toEqual([true, true, false]);
  });

  it('revealing a hidden column renders its header', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Stock');

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Stock')),
    ).toBeTrue();
  });

  it('persists toggles and restores them for a new table with the same stateKey', async () => {
    const first = createFixture(HostWithColumnsStateKeyComponent);
    await openChooser(first);
    clickOption(first, 'Name'); // hide Name
    clickOption(first, 'Stock'); // reveal Stock
    first.destroy();
    TestBed.resetTestingModule();

    const second = createFixture(HostWithColumnsStateKeyComponent);

    const headers = headerTexts(second.nativeElement);
    expect(headers.some((h) => h.includes('Name'))).toBeFalse();
    expect(headers.some((h) => h.includes('Stock'))).toBeTrue();
  });

  it('stores the visibility override in localStorage under `${stateKey}:columns`', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(
      localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`),
    ).toBeTruthy();
  });

  it('reset restores declared defaults and clears the stored override', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name'); // hide Name
    clickOption(fixture, 'Stock'); // reveal Stock

    const reset = chooserReset(fixture);
    expect(reset).withContext('reset button should render').toBeTruthy();
    reset!.click();
    fixture.detectChanges();

    const headers = headerTexts(fixture.nativeElement);
    expect(headers.some((h) => h.includes('Name'))).toBeTrue();
    expect(headers.some((h) => h.includes('Stock'))).toBeFalse();
    expect(localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`)).toBeNull();
  });

  it('hiding a visible column removes its header', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Name')),
    ).toBeFalse();
  });
});

describe('SpiderlyDataTableComponent — chooser styles survive the body teleport', () => {
  // Guards the `:host` overlay-styling trap — see .claude/rules/library-scss.md.
  // One pin per SCSS rule; add a row when a chooser rule is added.
  const stylePins: [selector: string, property: string, expected: string][] = [
    ['.column-chooser', 'display', 'flex'],
    ['.column-chooser', 'flex-direction', 'column'],
    ['.column-chooser-option', 'display', 'flex'],
    ['.column-chooser-reset', 'border-top-style', 'none'],
  ];

  it('keeps every chooser rule matching inside the teleported popover', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);

    const container = chooserContainer(fixture)!;
    for (const [selector, property, expected] of stylePins) {
      const el = container.querySelector<HTMLElement>(selector);
      expect(el).withContext(`${selector} should render`).toBeTruthy();
      expect(getComputedStyle(el!).getPropertyValue(property))
        .withContext(`${selector} { ${property} }`)
        .toBe(expected);
    }
  });
});

describe('SpiderlyDataTableComponent — visibility guards', () => {
  it('shows a lockVisible column as checked and disabled', async () => {
    const fixture = createFixture(HostWithLockedColumnComponent);

    await openChooser(fixture);

    const locked = chooserOptions(fixture).find((o) => o.label === 'Id')!;
    expect(locked.input.checked).toBeTrue();
    expect(locked.input.disabled).toBeTrue();
  });

  it('disables hiding the last visible data column', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name'); // hide Name — Id is now the only visible data column
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      chooserOptions(fixture).find((o) => o.label === 'Id')!.input.disabled,
    ).toBeTrue();

    clickOption(fixture, 'Stock'); // reveal Stock — Id is hideable again
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      chooserOptions(fixture).find((o) => o.label === 'Id')!.input.disabled,
    ).toBeFalse();
  });
});

// The safety net for all four gestures. Until the header menu arrived, "reset to default" only
// had visibility to undo; now an operator can also wrap, move and resize, and a layout with no
// way back is worse than one with no knobs.
describe('SpiderlyDataTableComponent — resetting the layout', () => {
  it('undoes wrap, order and width, not just visibility', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    const declared = dataTable.cols.map((col) => col.field);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-right"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).not.toEqual(declared);

    await openChooser(fixture);
    chooserContainer(fixture)!
      .querySelector<HTMLButtonElement>('[data-testid="column-chooser-reset"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual(declared);
    expect(dataTable.isColumnWrapped(dataTable.cols[0])).toBeFalse();
    // Nothing left in storage either: a reset that leaves the old layout behind comes back on
    // the next reload.
    expect(
      localStorage.getItem(`${dataTable.resolvedStateKey}:layout`),
    ).toBeNull();
  });

});

// The other half of decision 10, and the reason a view is more than a saved filter: a picking view
// and a payments view want different COLUMNS, not just different rows. Layout global to the table
// would re-create the original complaint one level up.
describe('SpiderlyDataTableComponent — layout is scoped to the view', () => {
  it('keeps each view\'s wrapped columns apart', async () => {
    const { fixture, dataTable } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    await clickView(fixture, 0);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.isColumnWrapped(dataTable.cols[0])).toBeTrue();

    await clickView(fixture, 1);

    expect(dataTable.isColumnWrapped(dataTable.cols[0]))
      .withContext('the other view must not inherit it')
      .toBeFalse();

    await clickView(fixture, 0);

    expect(dataTable.isColumnWrapped(dataTable.cols[0]))
      .withContext('and going back must find it again')
      .toBeTrue();
  });
});


// Decision 10's columns half (spiderly#431): a work-list view wants a "why is this row here"
// column the catch-all view has no use for, in the place of one it does not need.
describe('SpiderlyDataTableComponent — a view declares its columns', () => {
  it('shows the columns its view declares, and the table defaults elsewhere', async () => {
    const { fixture } = createWithDataTable(HostWithViewColumnsComponent);
    await renderRows(fixture);

    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Lokacija']);

    await clickView(fixture, 1);
    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Zašto']);

    await clickView(fixture, 0);
    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Lokacija']);
  });

  it('lets the operator\'s own choice in a view win over its declaration, in that view only', async () => {
    const first = createWithDataTable(HostWithViewColumnsComponent);
    await renderRows(first.fixture);
    await clickView(first.fixture, 1);

    await openChooser(first.fixture);
    clickOption(first.fixture, 'Zašto'); // hide what the view declares shown
    expect(shownHeaders(first.fixture)).toEqual(['Naziv']);

    await clickView(first.fixture, 0);
    expect(shownHeaders(first.fixture))
      .withContext('the other view keeps its own columns')
      .toEqual(['Naziv', 'Lokacija']);

    const next = await remount(first.fixture, HostWithViewColumnsComponent);
    await clickView(next.fixture, 1);
    expect(shownHeaders(next.fixture))
      .withContext('the choice survives a reload')
      .toEqual(['Naziv']);
  });

  // The operator's layout is a DELTA against the view's declaration, as it already is against
  // `Column.visible`: a choice equal to the declaration is not a choice, so a later change to the
  // declaration still reaches everyone who never moved that column.
  it('stores nothing for a column put back to its view declaration, and Reset returns to it', async () => {
    const { fixture } = createWithDataTable(HostWithViewColumnsComponent);
    await renderRows(fixture);
    await clickView(fixture, 1);

    await openChooser(fixture);
    clickOption(fixture, 'Zašto');
    clickOption(fixture, 'Zašto');
    expect(localStorage.getItem('sdt-view-columns-spec:late:columns')).toBeNull();

    clickOption(fixture, 'Lokacija');
    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Lokacija', 'Zašto']);

    chooserReset(fixture)!.click();
    fixture.detectChanges();
    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Zašto']);
  });

  // Views built from a server answer are rebuilt whenever it changes, and the grid has to follow
  // the declaration it is handed now rather than the one it entered the view with.
  it('follows a [views] change that redeclares the active view\'s columns', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewColumnsComponent);
    await renderRows(fixture);
    await clickView(fixture, 1);

    await fixture.ngZone!.run(async () => {
      host.views = host.views.map((view) =>
        view.id === 'late' ? { ...view, columns: { why: true } } : view,
      );
      fixture.detectChanges();
    });
    await renderRows(fixture);

    expect(shownHeaders(fixture)).toEqual(['Naziv', 'Lokacija', 'Zašto']);
  });
});


// A table migrated to the bar reads the same PrimeNG state key its header-filter days wrote, and
// that blob can still hold constraint meta. Under the store those constraints never reach a
// request (the payload replaces them), so revealing a hidden column for them resurrects a phantom
// — and on a localStorage table it does so on EVERY load, because nothing ever rewrites the stale
// blob. A consuming app's order list was exactly this: stateStorage 'local', operators with weeks of
// header-filter state.
describe('SpiderlyDataTableComponent — a store table ignores stale header-filter state', () => {
  it('does not reveal a hidden column for persisted header-filter meta', () => {
    sessionStorage.setItem(
      'sdt-store-no-phantom-reveal-spec',
      JSON.stringify({
        filters: { name: [{ value: 'abc', matchMode: 'contains' }] },
      }),
    );

    const fixture = createFixture(HostWithHiddenColumnFilterStoreComponent);

    expect(
      headerTexts(fixture.nativeElement).some((header) =>
        header.includes('Naziv'),
      ),
    )
      .withContext('a constraint the store never sends must not reveal anything')
      .toBeFalse();
  });
});
