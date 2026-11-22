// Reordering by dragging a header (`HeaderDrag`), through the table.

import { ComponentFixture } from '@angular/core/testing';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  createWithDataTable,
  HostWithLockedColumnComponent,
  HostWithTwoColumnsComponent,
} from './spiderly-data-table.fixtures.spec';

// The shortcut for neighbours, after the menu path rather than instead of it: HTML5 dnd has no
// edge auto-scroll, so a long move belongs in a menu, and it has no keyboard path at all
// (decision 6).
describe('SpiderlyDataTableComponent — dragging a header', () => {
  function dragHeaderOnto(
    fixture: ComponentFixture<unknown>,
    from: number,
    to: number,
  ): void {
    const headers = (fixture.nativeElement as HTMLElement).querySelectorAll<
      HTMLElement
    >('thead th');
    const transfer = new DataTransfer();

    headers[from].dispatchEvent(
      new DragEvent('dragstart', { bubbles: true, dataTransfer: transfer }),
    );
    headers[to].dispatchEvent(
      new DragEvent('dragover', { bubbles: true, dataTransfer: transfer }),
    );
    headers[to].dispatchEvent(
      new DragEvent('drop', { bubbles: true, dataTransfer: transfer }),
    );
    fixture.detectChanges();
  }

  it('drops a column where it was dragged, and remembers it', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    dragHeaderOnto(fixture, 0, 1);
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual([
      'id',
      'name',
    ]);
    expect(
      JSON.parse(localStorage.getItem(`${dataTable.resolvedStateKey}:layout`)!)
        .order,
    ).toEqual(['id', 'name']);
  });

  // Same rule the menu enforces, and it has to be enforced twice because a drop is a different
  // entry point: the identity column anchors the row and is the one a horizontal scroll pins.
  // The reason this is ours rather than pReorderableColumn. PrimeNG arms the th for anything that
  // is not an INPUT, a TEXTAREA or its own resizer — so our menu chevron and our resize grip both
  // start a column drag, and reaching for a width silently reorders the grid instead.
  it('does not start a drag from the menu button or the resize grip', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    const el = fixture.nativeElement as HTMLElement;
    const before = dataTable.visibleCols.map((col) => col.field);

    for (const testid of ['column-menu', 'column-resizer']) {
      const handle = el.querySelectorAll<HTMLElement>(
        `[data-testid="${testid}"]`,
      )[0];
      handle.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
      fixture.detectChanges();

      const headers = el.querySelectorAll<HTMLElement>('thead th');
      const transfer = new DataTransfer();
      headers[0].dispatchEvent(
        new DragEvent('dragstart', { bubbles: true, dataTransfer: transfer }),
      );
      headers[1].dispatchEvent(
        new DragEvent('drop', { bubbles: true, dataTransfer: transfer }),
      );
      await renderRows(fixture);

      expect(dataTable.visibleCols.map((col) => col.field))
        .withContext(`a drag started from ${testid} must not reorder`)
        .toEqual(before);
      document.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
    }
  });

  it('refuses a drop that would put a column in front of the locked one', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithLockedColumnComponent,
    );
    await renderRows(fixture);

    const before = dataTable.visibleCols.map((col) => col.field);
    dragHeaderOnto(fixture, 1, 0);
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual(before);
  });
});
