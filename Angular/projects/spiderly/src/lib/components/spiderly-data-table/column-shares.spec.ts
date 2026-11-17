// A column's width as a share of the table (`column-shares.ts`), through the table.

import { Component } from '@angular/core';
import { paginated, renderRows } from '../../testing/spec-support.spec';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  FIXTURE_WIDTH_PX,
  createWithDataTable,
  HostWithoutActionsComponent,
  headerCells,
  HostWithSwappableRowsComponent,
  swapRowNameTo,
  renderStable,
  HostWithSelectionComponent,
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
class HostWithRowActionsComponent {
  cols: Column[] = [
    { name: 'Name', field: 'name', filterType: 'text' },
    { actions: [{ field: 'Details' }, { field: 'Delete' }] },
  ];
  getList = () => paginated([{ id: 1, name: 'Ana' }]);
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithMoreColumnsThanFitComponent {
  // Six text columns at the 12rem default = 72rem, well past FIXTURE_WIDTH_PX — the premise
  // this suite's scroll assert rests on, held by the pin rather than the Karma window.
  cols: Column[] = Array.from({ length: 6 }, (_, i) => ({
    name: `Col ${i}`,
    field: `c${i}`,
    filterType: 'text' as const,
  }));
  getList = () => paginated([{ id: 1 }]);
}

const LONG_NAME = 'Aleksandar Konstantinović-Petrović, Sremska Kamenica';

/** As RENDERED — the geometry the operator's eye tracks, not the style string we hand PrimeNG. */
function headerWidths(el: HTMLElement): number[] {
  return headerCells(el).map((th) => th.getBoundingClientRect().width);
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithMixedFilterTypesComponent {
  cols: Column[] = [
    { name: 'Name', field: 'name', filterType: 'text' },
    { name: 'Active', field: 'active', filterType: 'boolean' },
  ];
  // No rows on purpose: the widths under test come from the declarations, not the content.
  getList = emptyList;
}

describe('SpiderlyDataTableComponent — a table too wide for its container scrolls', () => {
  // The worry was that fixed layout sizes columns from the table's width, so a laptop would get
  // crushed columns where auto layout gave a horizontal scroll. It does not: a table whose
  // declared column widths exceed its container is widened to their sum by the browser, and the
  // wrapper's overflow-auto still scrolls. Measured 2026-08-29 — six 12rem columns rendered a
  // 1152px table inside a 740px container, each header exactly 192px. No `min-width` needed;
  // this test exists so that free property is not silently lost.
  it('keeps the table at least as wide as its columns need', async () => {
    const { fixture } = await renderStable(HostWithMoreColumnsThanFitComponent);
    const el = fixture.nativeElement as HTMLElement;

    const table = el.querySelector('table')!.getBoundingClientRect().width;
    const container = el
      .querySelector('.spiderly-table-container')!
      .getBoundingClientRect().width;

    expect(table).toBeGreaterThan(container);
  });
});

describe('SpiderlyDataTableComponent — columns carrying no value still get room', () => {
  // Both leaned on `width: 0rem` meaning "shrink to fit"; `getColWidth()` says why it no longer
  // does.
  it('leaves an actions column room for its icons', async () => {
    const { fixture } = await renderStable(HostWithRowActionsComponent);

    const headers = headerCells(fixture.nativeElement as HTMLElement);
    const actions = headers[headers.length - 1];
    expect(actions.getBoundingClientRect().width).toBeGreaterThan(0);
  });

  it('leaves the selection checkbox column a real width', async () => {
    const { fixture } = await renderStable(HostWithSelectionComponent);

    const [selection] = headerCells(fixture.nativeElement as HTMLElement);
    expect(selection.getBoundingClientRect().width).toBeGreaterThan(0);
  });
});

describe('SpiderlyDataTableComponent — Column.width', () => {
  it('overrides the filter type default, and only when declared', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getColWidth({ filterType: 'numeric' })).toBe('12rem');
    expect(dataTable.getColWidth({ filterType: 'text', width: '4rem' })).toBe(
      '4rem',
    );
  });

  // Every filterType needs a row in the width table, or it silently inherits the actions-column
  // reservation — which is what the switch this replaced did to `blob`.
  it('sizes a blob column for its thumbnail, not as an actions column', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getColWidth({ filterType: 'blob' })).toBe('5rem');
  });
});

describe('SpiderlyDataTableComponent — column widths ignore the content', () => {
  it('keeps every column the same width when the rows change', async () => {
    const created = await renderStable(HostWithSwappableRowsComponent);
    const el = created.fixture.nativeElement as HTMLElement;
    const before = headerWidths(el);

    await swapRowNameTo(created, LONG_NAME);

    expect(headerWidths(el)).toEqual(before);
  });
});

describe('SpiderlyDataTableComponent — column widths follow the declared filter type', () => {
  // Fixed layout splits the table into EQUAL shares for columns that declare no width, which
  // would throw away what the per-filterType defaults say: a boolean holds "Da"/"Ne" and a text
  // column holds a name. Same proportions as before, just no longer moved by the content.
  it('gives a boolean column less room than a text column', async () => {
    const { fixture } = createWithDataTable(HostWithMixedFilterTypesComponent);
    const el = await renderRows(fixture);

    // The surplus is shared in PROPORTION to the declared widths, so the ratio survives whatever
    // width the table ends up with — measured 12rem/8rem = 1.5 exactly. That is what makes these
    // defaults carry the same intent under fixed layout as the minimums did under auto.
    const [text, boolean] = headerWidths(el);
    expect(text / boolean).toBeCloseTo(12 / 8, 2);
  });
});
