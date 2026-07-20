import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter } from '@angular/router';
import {
  TranslocoTestingModule,
  TranslocoTestingOptions,
} from '@jsverse/transloco';
import { DialogService } from 'primeng/dynamicdialog';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';

import { Filter } from '../../entities/filter';
import { PaginatedResult } from '../../entities/paginated-result';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { ConfigServiceBase } from '../../services/config.service.base';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import {
  Column,
  SpiderlyDataTableComponent,
} from './spiderly-data-table.component';

function translocoTesting(): TranslocoTestingOptions {
  return {
    langs: { en: {} },
    translocoConfig: { availableLangs: ['en'], defaultLang: 'en' },
    preloadLangs: true,
  };
}

const cols: Column[] = [{ name: 'Id', field: 'id', filterType: 'numeric' }];

// delay(0) so the result lands after the initial change-detection pass, avoiding
// NG0100 from a synchronous lazy-load emission. The caption (under test) renders
// regardless of when the data resolves.
const emptyList = (): Observable<PaginatedResult> =>
  of({ data: [], totalRecords: 0 } as PaginatedResult).pipe(delay(0));

function createFixture<T>(host: new () => T): ComponentFixture<T> {
  TestBed.configureTestingModule({
    imports: [host, TranslocoTestingModule.forRoot(translocoTesting())],
    providers: [
      provideRouter([]),
      { provide: ConfigServiceBase, useValue: { defaultPageSize: 10 } },
      { provide: SpiderlyMessageService, useValue: {} },
      { provide: DialogService, useValue: {} },
    ],
  });
  const fixture = TestBed.createComponent(host);
  fixture.detectChanges();
  return fixture;
}

@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyDataTableActionsDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template spiderlyDataTableActions>
        <button type="button" data-testid="custom-action">Custom</button>
      </ng-template>
    </spiderly-data-table>
  `,
})
class HostWithActionsComponent {
  cols = cols;
  getList = emptyList;
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
class HostWithoutActionsComponent {
  cols = cols;
  getList = emptyList;
}

describe('SpiderlyDataTableComponent — toolbar actions projection slot', () => {
  it('renders the projected actions ahead of the built-in Clear Filters button', () => {
    const el: HTMLElement = createFixture(HostWithActionsComponent).nativeElement;

    const custom = el.querySelector('[data-testid="custom-action"]');
    const clearFilters = el
      .querySelector('.pi-filter-slash')
      ?.closest('button');

    expect(custom)
      .withContext('projected action button should render')
      .toBeTruthy();
    expect(clearFilters)
      .withContext('built-in Clear Filters button should render')
      .toBeTruthy();

    // DOCUMENT_POSITION_FOLLOWING means clearFilters appears *after* custom in DOM order.
    const relativePosition = custom!.compareDocumentPosition(clearFilters!);
    expect(relativePosition & Node.DOCUMENT_POSITION_FOLLOWING)
      .withContext('projected action should precede Clear Filters')
      .toBeTruthy();
  });

  it('renders nothing extra when no actions template is projected', () => {
    const el: HTMLElement = createFixture(HostWithoutActionsComponent).nativeElement;

    expect(el.querySelector('[data-testid="custom-action"]')).toBeNull();
    // The built-in toolbar still renders.
    expect(el.querySelector('.pi-filter-slash')).toBeTruthy();
  });
});

const DEFAULT_SORT_STATE_KEY = 'sdt-default-sort-spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [defaultSortField]="'id'"
      [defaultSortOrder]="1"
      [stateKey]="stateKey"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithDefaultSortComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  stateKey = DEFAULT_SORT_STATE_KEY;
  captured: Filter[] = [];
  // Snapshot the filter — PrimeNG mutates/reuses the lazy-load event object.
  getList = (filter: Filter): Observable<PaginatedResult> => {
    this.captured.push(JSON.parse(JSON.stringify(filter)));
    return emptyList();
  };
}

describe('SpiderlyDataTableComponent — declared default sort', () => {
  const ID_ASC = [{ field: 'id', order: 1 }];

  function setup(): {
    host: HostWithDefaultSortComponent;
    dataTable: SpiderlyDataTableComponent;
  } {
    const fixture = createFixture(HostWithDefaultSortComponent);
    const dataTable = fixture.debugElement.query(
      By.directive(SpiderlyDataTableComponent),
    ).componentInstance as SpiderlyDataTableComponent;
    return { host: fixture.componentInstance, dataTable };
  }

  afterEach(() => sessionStorage.clear());

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

  it('returns to the declared default when Clear filters wipes the sort', () => {
    const { host, dataTable } = setup();

    dataTable.clear(dataTable.table);

    expect(host.captured[host.captured.length - 1].multiSortMeta).toEqual(ID_ASC);
    expect(dataTable.table._multiSortMeta).toEqual(ID_ASC);
  });
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
      getList = (filter: Filter): Observable<PaginatedResult> => {
        captured.push(JSON.parse(JSON.stringify(filter)));
        return emptyList();
      };
    }

    createFixture(HostWithoutDefaultSortComponent);

    expect(captured.length).toBe(1);
    expect(captured[0].multiSortMeta ?? null).toBeNull();
  });
});
