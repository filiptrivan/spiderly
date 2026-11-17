// The pinned left edge (`FrozenEdge`), through the table.

import { Component } from '@angular/core';
import { ComponentFixture } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { paginated, renderRows } from '../../testing/spec-support.spec';
import { PaginatedResult } from '../../entities/paginated-result';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { cols, createWithDataTable } from './spiderly-data-table.fixtures.spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
      [deleteListFromTableObservableMethod]="deleteList"
    ></spiderly-data-table>
  `,
})
class HostWithFrozenIdentityComponent {
  cols: Column[] = [
    { name: 'Broj', field: 'number', filterType: 'text', lockVisible: true },
    { name: 'Kupac', field: 'name', filterType: 'text' },
  ];
  getList = (): Observable<PaginatedResult> =>
    paginated([{ id: 1, number: 'PA-1', name: 'Marko' }]);
  deleteList = () => of(null);
}

// Once the grid scrolls sideways a row loses its identity: "Intesa, plaćeno, 12.400" with no idea
// whose order it is. NN/g's Data Tables is explicit that the leftmost header column must lock in
// place, and `lockVisible` already names exactly that column in every table — so freezing needs
// no new API (decision 8).
describe('SpiderlyDataTableComponent — the frozen left edge', () => {
  // The measured offset's correction lands at animation-frame timing `whenStable` never awaits
  // and needs a `detectChanges` to reach the DOM — the microtask comment in the component's
  // `observeSelectionWidth()` says why. Settle until the header invariant holds; the body cells
  // bind the same `frozenOffsetPx`, so they converge with it, and on non-convergence the asserts
  // below fail loudly with the real values.
  async function settleFrozenOffset(
    fixture: ComponentFixture<unknown>,
  ): Promise<void> {
    const headers = (
      fixture.nativeElement as HTMLElement
    ).querySelectorAll<HTMLElement>('thead th');
    for (let i = 0; i < 10; i++) {
      if (getComputedStyle(headers[1]).left === `${headers[0].offsetWidth}px`) {
        return;
      }
      await new Promise((resolve) => requestAnimationFrame(resolve));
      fixture.detectChanges();
    }
  }

  it('pins the identity column and the selection box, in that order', async () => {
    const { fixture } = createWithDataTable(HostWithFrozenIdentityComponent);
    await renderRows(fixture);
    await settleFrozenOffset(fixture);

    const el = fixture.nativeElement as HTMLElement;
    const headers = el.querySelectorAll<HTMLElement>('thead th');
    const cells = el.querySelectorAll<HTMLElement>('tbody td');

    // The checkbox column first, hard against the edge.
    expect(getComputedStyle(headers[0]).position).toBe('sticky');
    expect(getComputedStyle(headers[0]).left).toBe('0px');

    // The identity column next, offset by exactly the checkbox column's width — a gap or an
    // overlap here is the whole failure mode.
    expect(getComputedStyle(headers[1]).position).toBe('sticky');
    expect(getComputedStyle(headers[1]).left).toBe(
      `${headers[0].offsetWidth}px`,
    );

    // The body follows the header, or the columns come apart as soon as the grid scrolls.
    expect(getComputedStyle(cells[1]).position).toBe('sticky');
    expect(getComputedStyle(cells[1]).left).toBe(`${cells[0].offsetWidth}px`);

    // Everything after it scrolls.
    expect(getComputedStyle(headers[2]).position).not.toBe('sticky');
  });

  // Why the pinned th must match a plain one — the header palette, per-cell hover, and the
  // pointer-crossing tint this fixes: the SCSS .frozen-column comment carries the telling.
  it('leaves the frozen header cells on the header palette, not the row one', async () => {
    const { fixture } = createWithDataTable(HostWithFrozenIdentityComponent);
    await renderRows(fixture);

    const el = fixture.nativeElement as HTMLElement;
    // The row token needs a value or the body-cell assert is vacuous (no PrimeNG preset in Karma).
    el.style.setProperty('--p-datatable-row-background', 'rgb(1, 2, 3)');

    const headers = el.querySelectorAll<HTMLElement>('thead th');
    const cells = el.querySelectorAll<HTMLElement>('tbody td');

    expect(getComputedStyle(headers[1]).backgroundColor).toBe(
      getComputedStyle(headers[2]).backgroundColor,
    );
    // The body cells keep the opaque row background — that is decision 8, not a leak.
    expect(getComputedStyle(cells[1]).backgroundColor).toBe('rgb(1, 2, 3)');
  });
});
