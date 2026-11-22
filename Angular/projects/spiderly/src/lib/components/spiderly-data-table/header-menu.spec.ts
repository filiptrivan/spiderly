// The header menu (`HeaderMenu`), through the table: its items, and sorting and filtering from it.

import { Component, computed } from '@angular/core';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { SpiderlyCellTemplateDirective } from '../../directives/spiderly-cell-template.directive';
import { createFilterStore, textFilter } from '../../filters/filter-store';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  createWithDataTable,
  columnMenu,
  HostWithLockedColumnComponent,
  nameAndIdCols,
  longNameRow,
  HostWithTwoColumnsComponent,
  openColumnMenu,
} from './spiderly-data-table.fixtures.spec';

// The consumer shape the wrap contract exists for: a projected template whose markup clamps
// itself, deferring to the cell custom properties the way a consuming app's order grid does.
@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyCellTemplateDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template spiderlyCellTemplate="name" let-displayValue="displayValue">
        <span data-testid="clamped-cell" class="consumer-clamp">{{
          displayValue
        }}</span>
      </ng-template>
    </spiderly-data-table>
  `,
  styles: `
    .consumer-clamp {
      display: block;
      overflow: var(--spiderly-cell-overflow, hidden);
      text-overflow: ellipsis;
      white-space: var(--spiderly-cell-white-space, nowrap);
    }
  `,
})
class HostWithClampedCellTemplateComponent {
  cols = nameAndIdCols;
  getList = longNameRow;
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
class HostWithUnsortableColumnComponent {
  cols: Column[] = [
    { name: 'Napomena', field: 'note', sortable: false },
    { name: 'Id', field: 'id', filterType: 'numeric' },
  ];
  getList = emptyList;
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
class HostWithLinkedFilterComponent {
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text', filterId: 'name' },
    { name: 'Id', field: 'id', filterType: 'numeric' },
  ];
  getList = emptyList;
}

// Hiding a column meant opening the chooser popover and hunting for its row. The column itself is
// where the gesture belongs, and the menu is the surface the width, order and wrap controls will
// share (decision 3).
describe('SpiderlyDataTableComponent — the column header menu', () => {
  it('hides a column from its own header', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual([
      'name',
      'id',
    ]);

    await openColumnMenu(fixture);

    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-hide"]')!
      .click();
    fixture.detectChanges();

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual(['id']);
  });

  // The th carries pSortableColumn, so a click that reaches it sorts. Opening a menu must not
  // reorder 71.629 rows on the way.
  it('does not sort the grid when the menu is opened', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLButtonElement>('[data-testid="column-menu"]')[0]
      .click();
    fixture.detectChanges();

    expect(dataTable.sortKeys).toEqual([]);
  });

  // The other half of the truncation complaint. The default is one clamped line and stays that
  // way (decision 9), but WHICH column gives up its row height is the operator's
  // call, not the author of `cols` — the same rule Notion follows, per column, from the same menu
  // that hides it. Asserted on the computed style rather than a class name: what matters is that
  // the text wraps.
  it('wraps a column from its header menu, and remembers the choice', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector('tbody .cell-text')!;

    expect(getComputedStyle(cell()).whiteSpace).toBe('nowrap');

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    expect(getComputedStyle(cell()).whiteSpace).toBe('normal');
    // Durable, like a hidden column: a layout choice re-made every morning is not a choice. Read
    // out of storage rather than off the live instance, which would pass with nothing persisted.
    const stored = JSON.parse(
      localStorage.getItem(`${dataTable.resolvedStateKey}:layout`)!,
    );
    expect(stored.wrap).toEqual({ name: true });
  });

  // It is a toggle, so the second click has to undo the first — and the menu has to SAY which way
  // it is set. A menu item whose label reads the same in both states leaves the operator to
  // discover by clicking, which on a wide column means re-laying the grid out to find out.
  it('unwraps on a second click, and shows which way it is set', async () => {
    const { fixture } = createWithDataTable(HostWithTwoColumnsComponent);
    await renderRows(fixture);

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector('tbody .cell-text')!;
    const wrapItem = () =>
      columnMenu(fixture).querySelector<HTMLButtonElement>(
        '[data-testid="column-menu-wrap"]',
      )!;

    await openColumnMenu(fixture);
    expect(wrapItem().getAttribute('aria-checked')).toBe('false');
    wrapItem().click();
    await renderRows(fixture);

    expect(getComputedStyle(cell()).whiteSpace).toBe('normal');

    await openColumnMenu(fixture);
    expect(wrapItem().getAttribute('aria-checked')).toBe('true');
    wrapItem().click();
    await renderRows(fixture);

    expect(getComputedStyle(cell()).whiteSpace).toBe('nowrap');
  });

  // The toggle must reach a PROJECTED cell too, or it is a silent no-op on a fully-templated
  // table (a consuming app's orders grid). How it crosses the encapsulation boundary: the td.cell-wrap
  // rule in this component's SCSS, which is the contract's one telling. Toggle-off is not
  // re-driven here — the class coming off the td is pinned by "unwraps on a second click" above,
  // and the fallback clamp is what the first assertion already measures.
  it('wraps a projected cell template whose clamp reads the cell custom properties', async () => {
    const { fixture } = createWithDataTable(HostWithClampedCellTemplateComponent);
    await renderRows(fixture);

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector(
        '[data-testid="clamped-cell"]',
      )!;

    expect(getComputedStyle(cell()).whiteSpace).toBe('nowrap');

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    expect(getComputedStyle(cell()).whiteSpace).toBe('normal');
    expect(getComputedStyle(cell()).overflow).toBe('visible');
  });

  // Tvoja tačka 4. The menu path before the drag one, deliberately: it is the only reorder that
  // works from a keyboard, and it reaches a column that is scrolled off the right edge.
  it('moves a column from its menu, and remembers the order', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual([
      'name',
      'id',
    ]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-right"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual([
      'id',
      'name',
    ]);

    const stored = JSON.parse(
      localStorage.getItem(`${dataTable.resolvedStateKey}:layout`)!,
    );
    expect(stored.order).toEqual(['id', 'name']);
  });

  // The identity column is the row's anchor and, once the left edge freezes, the thing a
  // horizontal scroll keeps in view. It does not move, and nothing moves in front of it.
  it('refuses to move a locked column, or anything past it', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithLockedColumnComponent,
    );
    await renderRows(fixture);

    const locked = dataTable.cols[0];
    expect(locked.lockVisible).toBeTrue();
    expect(dataTable.canMoveColumn(locked, -1)).toBeFalse();
    expect(dataTable.canMoveColumn(locked, 1)).toBeFalse();
    expect(dataTable.canMoveColumn(dataTable.cols[1], -1)).toBeFalse();
  });

});

// Clicking a header cycles through asc, desc and off, which is fine for one column and unreadable
// for a direction someone wants NOW — you click and look, and click again if it went the other
// way. Naming the direction removes the guess, and it is the only sort path that works from a
// keyboard.
describe('SpiderlyDataTableComponent — sorting from the header menu', () => {
  it('sorts in the named direction', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-sort-desc"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.sortKeys).toEqual([
      { field: 'name', label: 'Naziv', descending: true },
    ]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-sort-asc"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.sortKeys).toEqual([
      { field: 'name', label: 'Naziv', descending: false },
    ]);
  });

  // A column the backend has no sort case for answers with a 400 on every load, so the menu must
  // not offer the gesture at all — the same rule that keeps its header from being clickable.
  it('offers no sort on a column that cannot be sorted', async () => {
    const { fixture } = createWithDataTable(HostWithUnsortableColumnComponent);
    await renderRows(fixture);

    await openColumnMenu(fixture);

    expect(
      columnMenu(fixture).querySelector<HTMLButtonElement>(
        '[data-testid="column-menu-sort-asc"]',
      )!.disabled,
    ).toBeTrue();
  });
});

// The bar took the filter off the header, and the header is still where an operator looks for it
// — twenty-seven tables' worth of habit. This is the shortcut back, and the only thing in the
// design that needs a column to KNOW its filter (decision 1's filterId, deliberately unbuilt
// until something asked for it).
describe('SpiderlyDataTableComponent — filtering from the header menu', () => {
  it('opens the bar editor for the column that declares a filter', async () => {
    const { fixture } = createWithDataTable(HostWithLinkedFilterComponent);
    await renderRows(fixture);

    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('[data-testid="filter-editor"]')).toBeNull();

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-filter"]')!
      .click();
    await renderRows(fixture);

    const editor = el.querySelector('[data-testid="filter-editor"]');
    expect(editor).not.toBeNull();
    expect(editor!.textContent).toContain('Naziv');
  });

  it('offers nothing to a column that declares no filter', async () => {
    const { fixture } = createWithDataTable(HostWithLinkedFilterComponent);
    await renderRows(fixture);

    await openColumnMenu(fixture, 1);

    expect(
      columnMenu(fixture).querySelector<HTMLButtonElement>(
        '[data-testid="column-menu-filter"]',
      )!.disabled,
    ).toBeTrue();
  });
});
