// What a click in the body does (`RowActions`), through the table: row navigation and the
// interactive cells that keep their own click.

import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router } from '@angular/router';
import { paginated } from '../../testing/spec-support.spec';
import { Column, SpiderlyDataTableComponent } from './spiderly-data-table.component';
import { cols, renderStable, clickRowCheckbox } from './spiderly-data-table.fixtures.spec';

const clonedIds: number[] = [];

// Every interactive surface a row can hold, on a navigating table: selection checkbox, an
// action icon, an onCellClick cell, and an editable cell — plus a plain cell that must navigate.
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [navigateOnRowClick]="true"
      [rowNavigationPath]="'/product-list'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithNavigatingRowsComponent {
  cols: Column[] = [
    { name: 'Name', field: 'name', filterType: 'text' },
    {
      name: 'Total',
      field: 'total',
      filterType: 'numeric',
      onCellClick: () => {},
    },
    {
      actions: [
        {
          field: 'custom',
          name: 'Clone',
          icon: 'pi pi-copy',
          onClick: (e: any) => clonedIds.push(e.id),
        },
      ],
    } as Column,
  ];
  getList = () => paginated([{ id: 7, name: 'Ana', total: 5 }]);
}

// Same table keyed by a non-default idField, which is what pins the id resolution itself.
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [idField]="'productId'"
      [navigateOnRowClick]="true"
      [rowNavigationPath]="'/product-list'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithCustomIdFieldComponent {
  cols: Column[] = [{ name: 'Name', field: 'name', filterType: 'text' }];
  getList = () => paginated([{ productId: 7, name: 'Ana' }]);
}

describe('SpiderlyDataTableComponent — row navigation vs interactive cells', () => {
  async function renderNavigatingTable() {
    clonedIds.length = 0;
    const created = await renderStable(HostWithNavigatingRowsComponent);
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigateByUrl').and.stub();
    return { ...created, navigate };
  }

  const cellAt = (fixture: ComponentFixture<unknown>, selector: string) =>
    (fixture.nativeElement as HTMLElement).querySelector(
      selector,
    ) as HTMLElement;

  it('navigates on a plain cell click', async () => {
    const { fixture, navigate } = await renderNavigatingTable();

    cellAt(fixture, 'tbody td:not(.row-interactive)').click();

    expect(navigate).toHaveBeenCalledWith('/product-list/7');
  });

  // The id comes from idField, not a hardcoded `id` — a table keyed by anything else used to
  // resolve `row?.id` to undefined and silently never navigate at all.
  it('navigates using the configured idField', async () => {
    const { fixture } = await renderStable(HostWithCustomIdFieldComponent);
    const navigate = spyOn(TestBed.inject(Router), 'navigateByUrl').and.stub();

    cellAt(fixture, 'tbody td').click();

    expect(navigate).toHaveBeenCalledWith('/product-list/7');
  });

  it('runs an action without also navigating away', async () => {
    const { fixture, navigate } = await renderNavigatingTable();

    cellAt(fixture, 'tbody span.pi-copy').click();

    expect(clonedIds).toEqual([7]);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('selects a row without also navigating away', async () => {
    const { fixture, dataTable, navigate } = await renderNavigatingTable();

    clickRowCheckbox(fixture, 0);

    expect(dataTable.newlySelectedItems).toEqual([7]);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('leaves an onCellClick cell to its own handler', async () => {
    const { fixture, navigate } = await renderNavigatingTable();

    cellAt(fixture, 'tbody td.clickable').click();

    expect(navigate).not.toHaveBeenCalled();
  });
});
