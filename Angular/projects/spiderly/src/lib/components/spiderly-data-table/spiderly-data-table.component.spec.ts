import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import {
  TranslocoTestingModule,
  TranslocoTestingOptions,
} from '@jsverse/transloco';
import { DialogService } from 'primeng/dynamicdialog';
import { Popover } from 'primeng/popover';
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
      provideNoopAnimations(),
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
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
    { name: 'Stock', field: 'stock', filterType: 'numeric', visible: false },
    { actions: [{ name: 'Details', field: 'Details' }] },
  ];
  getList = emptyList;
}

const COLUMNS_STATE_KEY = 'sdt-columns-spec';

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
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
    { name: 'Stock', field: 'stock', filterType: 'numeric', visible: false },
    { actions: [{ name: 'Details', field: 'Details' }] },
  ];
  stateKey = COLUMNS_STATE_KEY;
  getList = emptyList;
}

function headerTexts(el: HTMLElement): string[] {
  return Array.from(el.querySelectorAll('th')).map(
    (th) => th.textContent?.trim() ?? '',
  );
}

// Async because the checkboxes' [ngModel] writes resolve in a microtask.
async function openChooser(fixture: ComponentFixture<unknown>): Promise<void> {
  const button = (
    fixture.nativeElement as HTMLElement
  ).querySelector<HTMLButtonElement>('[data-testid="column-chooser-button"]');
  expect(button)
    .withContext('column chooser toolbar button should render')
    .toBeTruthy();
  button!.click();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

// Query THIS fixture's popover container — once open, PrimeNG appends it to
// document.body, where stale popovers from earlier fixtures may also linger.
function chooserOptions(
  fixture: ComponentFixture<unknown>,
): { label: string; input: HTMLInputElement }[] {
  const popover = fixture.debugElement.query(By.directive(Popover))
    .componentInstance as Popover;
  const container: HTMLElement | undefined = popover.container;
  return Array.from(
    container?.querySelectorAll('[data-testid="column-chooser-option"]') ?? [],
  ).map((row) => ({
    label: row.textContent?.trim() ?? '',
    input: row.querySelector<HTMLInputElement>('input[type="checkbox"]')!,
  }));
}

describe('SpiderlyDataTableComponent — column visibility', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

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
    chooserOptions(fixture)
      .find((o) => o.label === 'Stock')!
      .input.click();
    fixture.detectChanges();

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Stock')),
    ).toBeTrue();
  });

  it('persists toggles and restores them for a new table with the same stateKey', async () => {
    const first = createFixture(HostWithColumnsStateKeyComponent);
    await openChooser(first);
    chooserOptions(first)
      .find((o) => o.label === 'Name')!
      .input.click(); // hide Name
    chooserOptions(first)
      .find((o) => o.label === 'Stock')!
      .input.click(); // reveal Stock
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
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click();

    expect(
      localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`),
    ).toBeTruthy();
  });

  it('reset restores declared defaults and clears the stored override', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click(); // hide Name
    chooserOptions(fixture)
      .find((o) => o.label === 'Stock')!
      .input.click(); // reveal Stock
    fixture.detectChanges();

    const reset = fixture.debugElement
      .query(By.directive(Popover))
      .componentInstance.container?.querySelector(
        '[data-testid="column-chooser-reset"]',
      ) as HTMLButtonElement;
    expect(reset).withContext('reset button should render').toBeTruthy();
    reset.click();
    fixture.detectChanges();

    const headers = headerTexts(fixture.nativeElement);
    expect(headers.some((h) => h.includes('Name'))).toBeTrue();
    expect(headers.some((h) => h.includes('Stock'))).toBeFalse();
    expect(localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`)).toBeNull();
  });

  it('hiding a visible column removes its header', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click();
    fixture.detectChanges();

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Name')),
    ).toBeFalse();
  });
});

describe('SpiderlyDataTableComponent — hidden-but-constrained reconciliation on load', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('reveals hidden columns that persisted state still filters or sorts by', () => {
    // User hid Name; Stock is hidden by declaration.
    localStorage.setItem(
      `${COLUMNS_STATE_KEY}:columns`,
      JSON.stringify({ name: false }),
    );
    // But the persisted table state still filters by Name and sorts by Stock —
    // rendering either constraint invisibly would restrict/order data with no visible cause.
    sessionStorage.setItem(
      COLUMNS_STATE_KEY,
      JSON.stringify({
        filters: { name: [{ value: 'abc', matchMode: 'contains' }] },
        multiSortMeta: [{ field: 'stock', order: 1 }],
      }),
    );

    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    const headers = headerTexts(fixture.nativeElement);
    expect(headers.some((h) => h.includes('Name')))
      .withContext('filtered column must be revealed')
      .toBeTrue();
    expect(headers.some((h) => h.includes('Stock')))
      .withContext('sorted column must be revealed')
      .toBeTrue();
  });
});

describe('SpiderlyDataTableComponent — visibility guards', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('shows a lockVisible column as checked and disabled', async () => {
    @Component({
      imports: [SpiderlyDataTableComponent],
      template: `
        <spiderly-data-table
          [cols]="cols"
          [getPaginatedListObservableMethod]="getList"
        ></spiderly-data-table>
      `,
    })
    class HostWithLockedColumnComponent {
      cols: Column[] = [
        { name: 'Id', field: 'id', filterType: 'numeric', lockVisible: true },
        { name: 'Name', field: 'name', filterType: 'text' },
      ];
      getList = emptyList;
    }

    const fixture = createFixture(HostWithLockedColumnComponent);

    await openChooser(fixture);

    const locked = chooserOptions(fixture).find((o) => o.label === 'Id')!;
    expect(locked.input.checked).toBeTrue();
    expect(locked.input.disabled).toBeTrue();
  });

  it('disables hiding the last visible data column', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click(); // hide Name — Id is now the only visible data column
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      chooserOptions(fixture).find((o) => o.label === 'Id')!.input.disabled,
    ).toBeTrue();

    chooserOptions(fixture)
      .find((o) => o.label === 'Stock')!
      .input.click(); // reveal Stock — Id is hideable again
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      chooserOptions(fixture).find((o) => o.label === 'Id')!.input.disabled,
    ).toBeFalse();
  });
});

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [stateKey]="'sdt-clear-on-hide-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostForClearOnHideComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
    { name: 'Stock', field: 'stock', filterType: 'numeric' },
  ];
  captured: Filter[] = [];
  // Snapshot the filter — PrimeNG mutates/reuses the lazy-load event object.
  getList = (filter: Filter): Observable<PaginatedResult> => {
    this.captured.push(JSON.parse(JSON.stringify(filter)));
    return emptyList();
  };
}

describe('SpiderlyDataTableComponent — hiding a column clears its filter and sort', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  function setup(): {
    fixture: ComponentFixture<HostForClearOnHideComponent>;
    host: HostForClearOnHideComponent;
    dataTable: SpiderlyDataTableComponent;
  } {
    const fixture = createFixture(HostForClearOnHideComponent);
    const dataTable = fixture.debugElement.query(
      By.directive(SpiderlyDataTableComponent),
    ).componentInstance as SpiderlyDataTableComponent;
    return { fixture, host: fixture.componentInstance, dataTable };
  }

  it('clears the hidden column\'s filter and sort and reloads exactly once, without them', async () => {
    const { fixture, host, dataTable } = setup();

    dataTable.table.filter('abc', 'name', 'contains');
    dataTable.table.sort({
      originalEvent: new MouseEvent('click'),
      field: 'name',
    });
    fixture.detectChanges();
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click();
    fixture.detectChanges();

    expect(host.captured.length)
      .withContext('one reload after hiding a constrained column')
      .toBe(loadsBefore + 1);
    const last = host.captured[host.captured.length - 1];
    const nameFilters = (last.filters as any)?.['name'] ?? [];
    const nameFilterValues = (
      Array.isArray(nameFilters) ? nameFilters : [nameFilters]
    ).map((f: any) => f?.value ?? null);
    expect(nameFilterValues.every((v: any) => v === null))
      .withContext('hidden column must not filter')
      .toBeTrue();
    expect((last.multiSortMeta ?? []).some((m) => m.field === 'name'))
      .withContext('hidden column must not sort')
      .toBeFalse();
  });

  it('does not reload when hiding a column with no active filter or sort', async () => {
    const { fixture, host } = setup();
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Name')!
      .input.click();
    fixture.detectChanges();

    expect(host.captured.length).toBe(loadsBefore);
  });
});

describe('SpiderlyDataTableComponent — hiding the default-sort column', () => {
  afterEach(() => {
    sessionStorage.clear();
    localStorage.clear();
  });

  it('leaves the reload unsorted instead of re-applying an invisible default sort', async () => {
    @Component({
      imports: [SpiderlyDataTableComponent],
      template: `
        <spiderly-data-table
          [cols]="cols"
          [defaultSortField]="'id'"
          [stateKey]="'sdt-hidden-default-sort-spec'"
          [getPaginatedListObservableMethod]="getList"
        ></spiderly-data-table>
      `,
    })
    class HostWithHideableDefaultSortComponent {
      cols: Column[] = [
        { name: 'Id', field: 'id', filterType: 'numeric' },
        { name: 'Name', field: 'name', filterType: 'text' },
      ];
      captured: Filter[] = [];
      getList = (filter: Filter): Observable<PaginatedResult> => {
        this.captured.push(JSON.parse(JSON.stringify(filter)));
        return emptyList();
      };
    }

    const fixture = createFixture(HostWithHideableDefaultSortComponent);
    const host = fixture.componentInstance;
    expect(host.captured[0].multiSortMeta).toEqual([{ field: 'id', order: 1 }]);

    await openChooser(fixture);
    chooserOptions(fixture)
      .find((o) => o.label === 'Id')!
      .input.click();
    fixture.detectChanges();

    const last = host.captured[host.captured.length - 1];
    expect((last.multiSortMeta ?? []).some((m) => m.field === 'id'))
      .withContext('hidden default-sort column must not sort')
      .toBeFalse();
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
