// How a cell shows (`CellDisplay`), through the table: projected templates, the clamp, and the
// formatted values.

import { Component } from '@angular/core';
import { Observable, of } from 'rxjs';
import { paginated, renderRows } from '../../testing/spec-support.spec';
import { PaginatedResult } from '../../entities/paginated-result';
import { SpiderlyCellTemplateDirective } from '../../directives/spiderly-cell-template.directive';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  createFixture,
  createWithDataTable,
  HostWithoutActionsComponent,
  columnMenu,
  idAndNameCols,
  oneRow,
  HostWithSwappableRowsComponent,
  swapRowNameTo,
  renderStable,
  openColumnMenu,
} from './spiderly-data-table.fixtures.spec';

// ── per-column cell templates ────────────────────────────────────────────────
// The point of the slot is that a column opts in ALONE: everything not named by a template keeps
// rendering exactly as before, and the column's header and sort are untouched by it.

@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyCellTemplateDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template
        spiderlyCellTemplate="id"
        let-row
        let-value="value"
        let-displayValue="displayValue"
      >
        <span data-testid="custom-cell"
          >{{ displayValue }} / {{ value }} / {{ row.name }}</span
        >
      </ng-template>
    </spiderly-data-table>
  `,
})
class HostWithCellTemplateComponent {
  cols = idAndNameCols;
  getList = oneRow;
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
class HostWithoutCellTemplateComponent {
  cols = idAndNameCols;
  getList = oneRow;
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
class HostWithBooleanColumnsComponent {
  cols: Column[] = [
    {
      name: 'Eligible',
      field: 'eligible',
      filterType: 'boolean',
      triState: true,
    },
    { name: 'Disabled', field: 'disabled', filterType: 'boolean' },
  ];
  getList = () =>
    paginated([
      { eligible: true, disabled: true },
      { eligible: false, disabled: false },
      { eligible: null, disabled: null },
    ]);
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
class HostWithACrampedColumnComponent {
  cols: Column[] = [
    { name: 'Napomena', field: 'note', filterType: 'text', width: '4rem' },
    { name: 'Kratko', field: 'short', filterType: 'text', width: '20rem' },
  ];
  getList = (): Observable<PaginatedResult> =>
    paginated([
      { id: 1, note: 'kupac javio da nije kod kuce do petka, zvati posle 16h', short: 'ok' },
    ]);
}

describe('SpiderlyDataTableComponent — per-column cell templates', () => {
  // displayValue carries the formatting the table would have applied (so a template decorating a
  // value never re-implements it) and value the raw one — the same two words CellClickEvent uses,
  // which matters because one column can carry both a click handler and a template.
  it('renders the projected template, with the row and both forms of the value', async () => {
    const el = await renderRows(createFixture(HostWithCellTemplateComponent));

    const cell = el.querySelector('[data-testid="custom-cell"]');
    expect(cell).withContext('projected template should render').toBeTruthy();
    expect(cell!.textContent!.trim()).toBe('1,234.5 / 1234.5 / Ana');
  });

  it('leaves the columns it does not name alone', async () => {
    const el = await renderRows(createFixture(HostWithCellTemplateComponent));

    const cells = Array.from(el.querySelectorAll('tbody td')).map((td) =>
      td.textContent!.trim(),
    );
    expect(cells).toContain('Ana');
  });

  it('renders the built-in cell when no template is projected', async () => {
    const el = await renderRows(createFixture(HostWithoutCellTemplateComponent));

    const cells = Array.from(el.querySelectorAll('tbody td')).map((td) =>
      td.textContent!.trim(),
    );
    expect(cells).toContain('1,234.5');
    expect(el.querySelector('[data-testid="custom-cell"]')).toBeNull();
  });

  // The template replaces the CELL. If it ever swallowed the header the column would lose its
  // name and sort affordance, which is the one thing a data table must not trade for looks.
  it('leaves the templated column its header', async () => {
    const fixture = createFixture(HostWithCellTemplateComponent);
    const el = await renderRows(fixture);

    const headers = Array.from(el.querySelectorAll('thead th'));
    const idHeader = headers.find((th) => th.textContent!.includes('Id'))!;
    expect(idHeader.querySelector('p-sorticon')).toBeTruthy();
  });
});

describe('SpiderlyDataTableComponent — long text does not grow the row', () => {
  // The cost fixed layout hands over, and what `.cell-text` pays — the full telling is on that
  // rule in spiderly-data-table.component.scss. A 5000-character review comment is the real case.
  const PARAGRAPH = 'Lorem ipsum dolor sit amet '.repeat(60);

  it('keeps the row height the same when a cell holds far more text', async () => {
    const created = await renderStable(HostWithSwappableRowsComponent);
    const el = created.fixture.nativeElement as HTMLElement;
    const rowHeight = () =>
      el.querySelector('tbody tr')!.getBoundingClientRect().height;
    const before = rowHeight();

    await swapRowNameTo(created, PARAGRAPH);

    expect(rowHeight()).toBe(before);
  });
});

describe('SpiderlyDataTableComponent — multiselect cells show the label, not the raw value', () => {
  const severity: Column = {
    name: 'Severity',
    field: 'severityId',
    filterType: 'multiselect',
    dropdownOrMultiselectValues: [
      { label: 'Info', code: 1 },
      { label: 'Critical', code: 3 },
    ],
  };

  it('translates the stored value through the column options', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getRowData({ severityId: 1 }, severity)).toBe('Info');
    expect(dataTable.getRowData({ severityId: 3 }, severity)).toBe('Critical');
  });

  // The options arrive asynchronously in most apps (a namebook request that resolves after the
  // first paint), so an unmatched value must render as it did before rather than blanking the
  // column. Same for a value the list genuinely does not cover.
  it('falls back to the raw value when the options cannot name it', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    // Cast because the declared return is `string` while the untranslated branch hands back
    // whatever the row held — that mismatch predates this change and the fallback preserves it.
    expect(dataTable.getRowData({ severityId: 2 }, severity) as unknown).toBe(2);
    expect(
      dataTable.getRowData(
        { severityId: 1 },
        { ...severity, dropdownOrMultiselectValues: [] },
      ) as unknown,
    ).toBe(1);
  });

  it('leaves an empty cell empty', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getRowData({ severityId: null }, severity)).toBeNull();
    expect(dataTable.getRowData({}, severity)).toBeNull();
  });
});

describe('SpiderlyDataTableComponent — boolean cells', () => {
  const columnTexts = (el: HTMLElement, index: number) =>
    Array.from(el.querySelectorAll('tbody tr')).map((tr) =>
      tr.querySelectorAll('td')[index].textContent!.trim(),
    );

  it('shows null as an empty cell in a tri-state column', async () => {
    const el = await renderRows(createFixture(HostWithBooleanColumnsComponent));

    expect(columnTexts(el, 0)).toEqual(['Yes', 'No', '']);
  });

  it('shows null as No in a column that is not tri-state', async () => {
    const el = await renderRows(createFixture(HostWithBooleanColumnsComponent));

    expect(columnTexts(el, 1)).toEqual(['Yes', 'No', 'No']);
  });
});

// The clamp hides the value and, until now, offered nothing to read it with — which is why a consuming
// app hand-added [title] in four places. Wrapping is a choice someone makes; this is for the default
// nobody touched (decision 9). Only when it actually overflows: a title on a cell
// that fits is noise on every hover across a dense grid, which is what got the SKU tooltip
// removed in the first place.
describe('SpiderlyDataTableComponent — the clamped cell says what it hides', () => {
  it('titles a cell only while its text does not fit', async () => {
    const { fixture } = createWithDataTable(HostWithACrampedColumnComponent);
    await renderRows(fixture);

    const cells = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody .cell-text',
    );

    // Measured when the pointer arrives, which is the only moment a native title can surface —
    // and the reason there is no per-cell observer for it (see the directive).
    for (const cell of Array.from(cells)) {
      cell.dispatchEvent(new PointerEvent('pointerenter', { bubbles: false }));
    }
    fixture.detectChanges();

    expect(cells[0].getAttribute('title')).toBe(
      'kupac javio da nije kod kuce do petka, zvati posle 16h',
    );
    expect(cells[1].getAttribute('title')).toBeNull();
  });

  // The other escape from a cramped column, and the cheaper one: no drag, no aim. Asserted
  // through the overflow title rather than a number — what the operator wanted was to READ the
  // value, and the title disappearing is that, measured.
  it('fits a column to its widest cell', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithACrampedColumnComponent,
    );
    await renderRows(fixture);

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector('tbody .cell-text')!;
    const hover = () =>
      cell().dispatchEvent(new PointerEvent('pointerenter', { bubbles: false }));

    hover();
    expect(cell().getAttribute('title')).not.toBeNull();
    const before = dataTable.columnShare(dataTable.cols[0]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-fit"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.columnShare(dataTable.cols[0])).toBeGreaterThan(before);
    hover();
    expect(cell().getAttribute('title')).toBeNull();
  });

});
