// The checkbox column (`RowSelection`), through the table: shift+click ranges.

import { Component, computed } from '@angular/core';
import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  fourSelectableRows,
  renderStable,
  HostWithSelectionComponent,
  clickRowCheckbox,
} from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [hasLazyLoad]="false"
      [getFormArrayItems]="getItems"
    ></spiderly-data-table>
  `,
})
class HostWithClientSideSelectionComponent {
  cols = cols;
  getItems = () => [{ id: 1 }, { id: 2 }, { id: 3 }, { id: 4 }];
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [hasLazyLoad]="false"
      [rows]="10"
      [getFormArrayItems]="getItems"
    ></spiderly-data-table>
  `,
})
class HostWithPagedClientSideSelectionComponent {
  cols = cols;
  getItems = () =>
    Array.from({ length: 15 }, (unused, i) => ({ id: i + 1 }));
}

// Unsaved client-side rows carry a null id — the same value rangeAnchorId uses for "no anchor".
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [hasLazyLoad]="false"
      [getFormArrayItems]="getItems"
    ></spiderly-data-table>
  `,
})
class HostWithNullIdRowsComponent {
  cols = cols;
  getItems = () => [{ id: null }, { id: 2 }, { id: 3 }, { id: 4 }];
}

// isAllSelected stays null (the default tri-state) while the server reports its own selection.
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
class HostWithServerSelectionComponent {
  cols = cols;
  getList = fourSelectableRows;
  getSelected = () => of({ selectedIds: [2], totalRecordsSelected: 1 } as any);
}

describe('SpiderlyDataTableComponent — shift-click range selection', () => {
  const renderSelectionTable = () => renderStable(HostWithSelectionComponent);

  it('selects every row between the anchor and the shift-clicked checkbox', async () => {
    const { fixture, dataTable } = await renderSelectionTable();
    const selected: number[] = [];
    dataTable.onRowSelect.subscribe((e) => selected.push(e.id));

    clickRowCheckbox(fixture, 0);
    clickRowCheckbox(fixture, 3, { shift: true });

    expect(dataTable.newlySelectedItems).toEqual([1, 2, 3, 4]);
    expect(dataTable.rowsSelectedNumber).toBe(4);
    expect(selected).toEqual([1, 2, 3, 4]);
  });

  it('applies the clicked checkbox state, so a shift-uncheck clears the range', async () => {
    const { fixture, dataTable } = await renderSelectionTable();

    clickRowCheckbox(fixture, 0);
    clickRowCheckbox(fixture, 3, { shift: true }); // all four on
    clickRowCheckbox(fixture, 3); // uncheck the end; anchor moves there
    clickRowCheckbox(fixture, 0, { shift: true }); // shift-uncheck back to the top

    expect(dataTable.newlySelectedItems).toEqual([]);
    expect(dataTable.rowsSelectedNumber).toBe(0);
  });

  it('skips rows already in the target state — no duplicate delta entries', async () => {
    const { fixture, dataTable } = await renderSelectionTable();
    const selected: number[] = [];
    dataTable.onRowSelect.subscribe((e) => selected.push(e.id));

    clickRowCheckbox(fixture, 1); // id 2 pre-selected
    clickRowCheckbox(fixture, 0); // id 1 — the anchor
    clickRowCheckbox(fixture, 3, { shift: true });

    expect([...dataTable.newlySelectedItems].sort((a, b) => a - b)).toEqual([
      1, 2, 3, 4,
    ]);
    expect(dataTable.rowsSelectedNumber).toBe(4);
    // The range emitted only for ids 3 and 4 — 1 and 2 already held the target state.
    expect(selected).toEqual([2, 1, 3, 4]);
  });

  it('treats a shift-click with no anchor as a plain toggle', async () => {
    const { fixture, dataTable } = await renderSelectionTable();

    clickRowCheckbox(fixture, 2, { shift: true });

    expect(dataTable.newlySelectedItems).toEqual([3]);
    expect(dataTable.rowsSelectedNumber).toBe(1);
  });

  // Every lazy page flip, sort and filter goes through lazyLoad, so the reset there is what
  // stops a range from spanning a server-side page. (The cross-page case where the anchor id is
  // simply absent from the new page is covered structurally by renderedRows() — the off-window
  // spec below pins that half.)
  it('drops the anchor on any lazy reload', async () => {
    const { fixture, dataTable } = await renderSelectionTable();

    clickRowCheckbox(fixture, 0);
    dataTable.lazyLoad(dataTable.lastLazyLoadEvent);
    await renderRows(fixture);
    clickRowCheckbox(fixture, 3, { shift: true });

    // Anchor gone → the shift-click degrades to a plain toggle of id 4 alone.
    expect(dataTable.newlySelectedItems).toEqual([1, 4]);
    expect(dataTable.rowsSelectedNumber).toBe(2);
  });

  it('routes a shift-deselect under select-all into unselectedItems', async () => {
    const { fixture, dataTable } = await renderSelectionTable();

    dataTable.selectAll(true);
    fixture.detectChanges();
    clickRowCheckbox(fixture, 3); // uncheck id 4; anchor moves there
    clickRowCheckbox(fixture, 0, { shift: true });

    expect([...dataTable.unselectedItems].sort((a, b) => a - b)).toEqual([
      1, 2, 3, 4,
    ]);
    expect(dataTable.newlySelectedItems).toEqual([]);
    expect(dataTable.rowsSelectedNumber).toBe(0);
  });

  it('range-selects on a client-side (form-array) table too', async () => {
    const { fixture, dataTable } = await renderStable(
      HostWithClientSideSelectionComponent,
    );

    clickRowCheckbox(fixture, 0);
    clickRowCheckbox(fixture, 3, { shift: true });

    expect(dataTable.selectedItemIds).toEqual([1, 2, 3, 4]);
    expect(dataTable.rowsSelectedNumber).toBe(4);
  });

  // The server already reports id 2 as selected, so the range must not re-add it to the delta.
  it('keeps the delta consistent when a range sweeps server-selected rows', async () => {
    const { fixture, dataTable } = await renderStable(
      HostWithServerSelectionComponent,
    );

    clickRowCheckbox(fixture, 0); // id 1
    clickRowCheckbox(fixture, 3, { shift: true }); // range 1–4 over the pre-selected id 2

    expect(dataTable.newlySelectedItems).toEqual([1, 3, 4]); // id 2 was already selected
    expect(dataTable.unselectedItems).toEqual([]);
    expect(dataTable.rowsSelectedNumber).toBe(4);
  });

  it('treats a null id as "no anchor", never as a row to range from', async () => {
    const { fixture, dataTable } = await renderStable(HostWithNullIdRowsComponent);

    clickRowCheckbox(fixture, 0); // the null-id row — must not become a usable anchor
    clickRowCheckbox(fixture, 3, { shift: true }); // id 4

    expect(dataTable.selectedItemIds).toEqual([null, 4] as any);
    expect(dataTable.rowsSelectedNumber).toBe(2);
  });

  // A press on the cell around the checkbox produces no change event, so it must not arm the
  // range at all — otherwise the flag strands there and the next toggle of that row ranges.
  it('never arms a range from a press beside the checkbox', async () => {
    const { fixture, dataTable } = await renderSelectionTable();
    const cells = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody td.selection-cell',
    );

    clickRowCheckbox(fixture, 0); // anchor id 1
    cells[3].dispatchEvent(
      new MouseEvent('mousedown', {
        bubbles: true,
        cancelable: true,
        shiftKey: true,
      }),
    );
    // Then toggle that same row from the keyboard (no press of its own).
    (cells[3].querySelector('input[type="checkbox"]') as HTMLInputElement).click();
    fixture.detectChanges();

    expect(dataTable.newlySelectedItems).toEqual([1, 4]); // plain toggle, no 1–4 range
  });

  it('never lets a shift-press on one row range from a toggle of another', async () => {
    const { fixture, dataTable } = await renderSelectionTable();

    clickRowCheckbox(fixture, 0); // anchor id 1
    // Shift-press row 1's checkbox without completing the click there (aborted gesture)…
    const cells = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody td.selection-cell',
    );
    cells[1].querySelector('input[type="checkbox"]')!.dispatchEvent(
      new MouseEvent('mousedown', {
        bubbles: true,
        cancelable: true,
        shiftKey: true,
      }),
    );
    // …then toggle row 3's checkbox with no press of its own (the keyboard path).
    (cells[3].querySelector('input[type="checkbox"]') as HTMLInputElement).click();
    fixture.detectChanges();

    expect(dataTable.newlySelectedItems).toEqual([1, 4]); // plain toggle, no 1–4 range
  });

  it('degrades to a plain toggle when the anchor left the rendered window', async () => {
    const { fixture, dataTable } = await renderStable(
      HostWithPagedClientSideSelectionComponent,
    );

    clickRowCheckbox(fixture, 0); // anchor id 1, page 1
    dataTable.table.first = 10; // flip to page 2 (rows 11–15)
    fixture.detectChanges();
    clickRowCheckbox(fixture, 2, { shift: true }); // id 13; anchor not visible

    expect(dataTable.selectedItemIds).toEqual([1, 13]);
    expect(dataTable.rowsSelectedNumber).toBe(2);
  });

  // The belt behind onSelectionCellMouseDown's preventDefault. Its neighbours in the SCSS use
  // ::ng-deep and this rule does not, so a computed-style assert is what proves it still lands.
  it('keeps the selection cell unselectable in the rendered table', async () => {
    const { fixture } = await renderSelectionTable();
    const cell = (fixture.nativeElement as HTMLElement).querySelector(
      'tbody td.selection-cell',
    ) as HTMLElement;

    expect(getComputedStyle(cell).getPropertyValue('user-select')).toBe('none');
  });

  it('suppresses the browser text selection a shift-press would start', async () => {
    const { fixture } = await renderSelectionTable();
    const cell = (fixture.nativeElement as HTMLElement).querySelector(
      'tbody td.selection-cell',
    ) as HTMLElement;

    const shiftPress = new MouseEvent('mousedown', {
      bubbles: true,
      cancelable: true,
      shiftKey: true,
    });
    const plainPress = new MouseEvent('mousedown', {
      bubbles: true,
      cancelable: true,
    });
    cell.dispatchEvent(shiftPress);
    cell.dispatchEvent(plainPress);

    expect(shiftPress.defaultPrevented).toBe(true);
    expect(plainPress.defaultPrevented).toBe(false);
  });
});
