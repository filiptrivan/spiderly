// A column's width from a drag of its grip (`ColumnResize`), through the table.

import { ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  createWithDataTable,
  HostWithTwoColumnsComponent,
} from './spiderly-data-table.fixtures.spec';

// Filip's second complaint. Widths are SHARES, not pixels: under the fixed layout the browser
// splits surplus in proportion to them, so storing pixels would pin a column and stop it
// answering the window (decision 4). A drag therefore trades share between the two columns it
// sits between, which is `fit` semantics kept in the model the table already uses.
describe('SpiderlyDataTableComponent — column widths', () => {
  function dragResizer(
    fixture: ComponentFixture<unknown>,
    index: number,
    byPx: number,
  ): void {
    const grip = (fixture.nativeElement as HTMLElement).querySelectorAll<
      HTMLElement
    >('[data-testid="column-resizer"]')[index];

    grip.dispatchEvent(
      new MouseEvent('mousedown', { clientX: 300, bubbles: true }),
    );
    document.dispatchEvent(
      new MouseEvent('mousemove', { clientX: 300 + byPx, bubbles: true }),
    );
    document.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
    fixture.detectChanges();
  }

  it('trades share between neighbours on a drag, and remembers it', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    const before = dataTable.cols.map((col) => dataTable.columnShare(col));

    dragResizer(fixture, 0, 60);
    await renderRows(fixture);

    const after = dataTable.cols.map((col) => dataTable.columnShare(col));

    expect(after[0]).toBeGreaterThan(before[0]);
    expect(after[1]).toBeLessThan(before[1]);
    // The table keeps its total, so widening one column never starts a horizontal scroll on its
    // own — that only happens once the SUM of minimums stops fitting.
    expect(after[0] + after[1]).toBeCloseTo(before[0] + before[1], 3);

    const stored = JSON.parse(
      localStorage.getItem(`${dataTable.resolvedStateKey}:layout`)!,
    );
    expect(Object.keys(stored.widths).sort()).toEqual(['id', 'name']);
  });

  // The grip sits inside a th carrying pSortableColumn, and sorting hangs off CLICK — which
  // stopPropagation on mousedown does not touch. Without a guard, every resize also reorders the
  // grid, and on a large one the click lands on the th rather than on the grip.
  it('does not sort the grid when a column is resized', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    dragResizer(fixture, 0, 60);
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLElement>('th')[0]
      .dispatchEvent(new MouseEvent('click', { bubbles: true }));
    await renderRows(fixture);

    expect(dataTable.sortKeys).toEqual([]);
  });

});
