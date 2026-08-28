import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { DialogService } from 'primeng/dynamicdialog';
import { Popover } from 'primeng/popover';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';

import { translocoTesting } from '../../testing/spec-support.spec';
import { Filter } from '../../entities/filter';
import { PaginatedResult } from '../../entities/paginated-result';
import { SpiderlyCellTemplateDirective } from '../../directives/spiderly-cell-template.directive';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import { ConfigServiceBase } from '../../services/config.service.base';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import {
  Column,
  SpiderlyDataTableComponent,
} from './spiderly-data-table.component';

const cols: Column[] = [{ name: 'Id', field: 'id', filterType: 'numeric' }];

// delay(0) so the result lands after the initial change-detection pass, avoiding
// NG0100 from a synchronous lazy-load emission.
const paginated = (data: any[]): Observable<PaginatedResult> =>
  of({ data, totalRecords: data.length } as PaginatedResult).pipe(delay(0));

const emptyList = (): Observable<PaginatedResult> => paginated([]);

// Snapshot each filter — PrimeNG mutates/reuses the lazy-load event object.
const capturingGetList =
  (captured: Filter[]) =>
  (filter: Filter): Observable<PaginatedResult> => {
    captured.push(JSON.parse(JSON.stringify(filter)));
    return emptyList();
  };

// Every suite touches persisted table state; wipe both stores between tests.
afterEach(() => {
  sessionStorage.clear();
  localStorage.clear();
});

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

function createWithDataTable<T>(host: new () => T): {
  fixture: ComponentFixture<T>;
  host: T;
  dataTable: SpiderlyDataTableComponent;
} {
  const fixture = createFixture(host);
  const dataTable = fixture.debugElement.query(
    By.directive(SpiderlyDataTableComponent),
  ).componentInstance as SpiderlyDataTableComponent;
  return { fixture, host: fixture.componentInstance, dataTable };
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
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — declared default sort', () => {
  const ID_ASC = [{ field: 'id', order: 1 }];

  const setup = () => createWithDataTable(HostWithDefaultSortComponent);

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
  cols = idNameStockCols;
  getList = emptyList;
}

const COLUMNS_STATE_KEY = 'sdt-columns-spec';

// Id / Name visible, Stock hidden by declaration, plus an actions column.
const idNameStockCols: Column[] = [
  { name: 'Id', field: 'id', filterType: 'numeric' },
  { name: 'Name', field: 'name', filterType: 'text' },
  { name: 'Stock', field: 'stock', filterType: 'numeric', visible: false },
  { actions: [{ name: 'Details', field: 'Details' }] },
];

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
  cols = idNameStockCols;
  stateKey = COLUMNS_STATE_KEY;
  getList = emptyList;
}

function headerCells(el: HTMLElement): HTMLTableCellElement[] {
  return Array.from(el.querySelectorAll('th'));
}

function headerTexts(el: HTMLElement): string[] {
  return headerCells(el).map((th) => th.textContent?.trim() ?? '');
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
function chooserContainer(
  fixture: ComponentFixture<unknown>,
): HTMLElement | undefined {
  const popover = fixture.debugElement.query(By.directive(Popover))
    .componentInstance as Popover;
  return popover.container as HTMLElement | undefined;
}

function chooserOptions(
  fixture: ComponentFixture<unknown>,
): { label: string; input: HTMLInputElement }[] {
  return Array.from(
    chooserContainer(fixture)?.querySelectorAll(
      '[data-testid="column-chooser-option"]',
    ) ?? [],
  ).map((row) => ({
    label: row.textContent?.trim() ?? '',
    input: row.querySelector<HTMLInputElement>('input[type="checkbox"]')!,
  }));
}

function clickOption(fixture: ComponentFixture<unknown>, label: string): void {
  chooserOptions(fixture)
    .find((o) => o.label === label)!
    .input.click();
  fixture.detectChanges();
}

function chooserReset(
  fixture: ComponentFixture<unknown>,
): HTMLButtonElement | null {
  return (
    chooserContainer(fixture)?.querySelector<HTMLButtonElement>(
      '[data-testid="column-chooser-reset"]',
    ) ?? null
  );
}

describe('SpiderlyDataTableComponent — column visibility', () => {
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
    clickOption(fixture, 'Stock');

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Stock')),
    ).toBeTrue();
  });

  it('persists toggles and restores them for a new table with the same stateKey', async () => {
    const first = createFixture(HostWithColumnsStateKeyComponent);
    await openChooser(first);
    clickOption(first, 'Name'); // hide Name
    clickOption(first, 'Stock'); // reveal Stock
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
    clickOption(fixture, 'Name');

    expect(
      localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`),
    ).toBeTruthy();
  });

  it('reset restores declared defaults and clears the stored override', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name'); // hide Name
    clickOption(fixture, 'Stock'); // reveal Stock

    const reset = chooserReset(fixture);
    expect(reset).withContext('reset button should render').toBeTruthy();
    reset!.click();
    fixture.detectChanges();

    const headers = headerTexts(fixture.nativeElement);
    expect(headers.some((h) => h.includes('Name'))).toBeTrue();
    expect(headers.some((h) => h.includes('Stock'))).toBeFalse();
    expect(localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`)).toBeNull();
  });

  it('hiding a visible column removes its header', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(
      headerTexts(fixture.nativeElement).some((h) => h.includes('Name')),
    ).toBeFalse();
  });
});

describe('SpiderlyDataTableComponent — chooser styles survive the body teleport', () => {
  // Guards the `:host` overlay-styling trap — see Angular/CLAUDE.md → overlay styling.
  // One pin per SCSS rule; add a row when a chooser rule is added.
  const stylePins: [selector: string, property: string, expected: string][] = [
    ['.column-chooser', 'display', 'flex'],
    ['.column-chooser', 'flex-direction', 'column'],
    ['.column-chooser-option', 'display', 'flex'],
    ['.column-chooser-reset', 'border-top-style', 'none'],
  ];

  it('keeps every chooser rule matching inside the teleported popover', async () => {
    const fixture = createFixture(HostWithHiddenColumnComponent);

    await openChooser(fixture);

    const container = chooserContainer(fixture)!;
    for (const [selector, property, expected] of stylePins) {
      const el = container.querySelector<HTMLElement>(selector);
      expect(el).withContext(`${selector} should render`).toBeTruthy();
      expect(getComputedStyle(el!).getPropertyValue(property))
        .withContext(`${selector} { ${property} }`)
        .toBe(expected);
    }
  });
});

describe('SpiderlyDataTableComponent — hidden-but-constrained reconciliation on load', () => {
  it('never persists a reconciliation reveal — a later toggle keeps the stored choice intact', async () => {
    // User hid Name, but persisted state still filters by it → revealed on init.
    localStorage.setItem(
      `${COLUMNS_STATE_KEY}:columns`,
      JSON.stringify({ name: false }),
    );
    sessionStorage.setItem(
      COLUMNS_STATE_KEY,
      JSON.stringify({
        filters: { name: [{ value: 'abc', matchMode: 'contains' }] },
      }),
    );

    const fixture = createFixture(HostWithColumnsStateKeyComponent);
    await openChooser(fixture);
    clickOption(fixture, 'Stock'); // unrelated toggle — persists the override map

    const stored = JSON.parse(
      localStorage.getItem(`${COLUMNS_STATE_KEY}:columns`)!,
    );
    expect(stored['name'])
      .withContext('transient reveal must not overwrite the stored user choice')
      .toBe(false);
    expect(stored['stock']).toBeTrue();
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

describe('SpiderlyDataTableComponent — visibility guards', () => {
  it('shows a lockVisible column as checked and disabled', async () => {
    const fixture = createFixture(HostWithLockedColumnComponent);

    await openChooser(fixture);

    const locked = chooserOptions(fixture).find((o) => o.label === 'Id')!;
    expect(locked.input.checked).toBeTrue();
    expect(locked.input.disabled).toBeTrue();
  });

  it('disables hiding the last visible data column', async () => {
    const fixture = createFixture(HostWithColumnsStateKeyComponent);

    await openChooser(fixture);
    clickOption(fixture, 'Name'); // hide Name — Id is now the only visible data column
    await fixture.whenStable();
    fixture.detectChanges();

    expect(
      chooserOptions(fixture).find((o) => o.label === 'Id')!.input.disabled,
    ).toBeTrue();

    clickOption(fixture, 'Stock'); // reveal Stock — Id is hideable again
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
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — hiding a column clears its filter and sort', () => {
  const setup = () => createWithDataTable(HostForClearOnHideComponent);

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
    clickOption(fixture, 'Name');

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
    clickOption(fixture, 'Name');

    expect(host.captured.length).toBe(loadsBefore);
  });
});

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
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — hiding the default-sort column', () => {
  it('leaves the reload unsorted instead of re-applying an invisible default sort', async () => {
    const fixture = createFixture(HostWithHideableDefaultSortComponent);
    const host = fixture.componentInstance;
    expect(host.captured[0].multiSortMeta).toEqual([{ field: 'id', order: 1 }]);

    await openChooser(fixture);
    clickOption(fixture, 'Id');

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
      getList = capturingGetList(captured);
    }

    createFixture(HostWithoutDefaultSortComponent);

    expect(captured.length).toBe(1);
    expect(captured[0].multiSortMeta ?? null).toBeNull();
  });
});

describe('SpiderlyDataTableComponent — CommaSeparated columns are not sortable', () => {
  // The backend's PaginatedResultGenerator never emits a sort case for *CommaSeparated collection
  // columns (the same naming convention decides both sides), and unknown sort fields are rejected
  // with a 400 — so the header must not offer the click. Before this rule, clicking such a header
  // (e.g. an admin SKU column) 500'd: Sentry BACKEND-RS-1F.

  @Component({
    imports: [SpiderlyDataTableComponent],
    template: `
      <spiderly-data-table
        [cols]="cols"
        [getPaginatedListObservableMethod]="getList"
      ></spiderly-data-table>
    `,
  })
  class HostWithCommaSeparatedColumnComponent {
    cols: Column[] = [
      { name: 'Title', field: 'title', filterType: 'text' },
      { name: 'Sku', field: 'productVariantsCommaSeparated', filterType: 'text' },
      { name: 'Notes', field: 'notes', filterType: 'text', sortable: false },
    ];
    getList = emptyList;
  }

  const headerOf = (
    fixture: ComponentFixture<unknown>,
    name: string,
  ): HTMLTableCellElement =>
    headerCells(fixture.nativeElement).find((th) =>
      (th.textContent ?? '').includes(name),
    )!;

  it('disables click-to-sort and hides the sort icon on a CommaSeparated field', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const skuHeader = headerOf(fixture, 'Sku');

    expect(skuHeader.classList.contains('p-datatable-sortable-column')).toBeFalse();
    expect(skuHeader.querySelector('p-sorticon')).toBeNull();
  });

  it('keeps ordinary columns sortable', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const titleHeader = headerOf(fixture, 'Title');

    expect(titleHeader.classList.contains('p-datatable-sortable-column')).toBeTrue();
    expect(titleHeader.querySelector('p-sorticon')).not.toBeNull();
  });

  it('still honors an explicit sortable: false', () => {
    const fixture = createFixture(HostWithCommaSeparatedColumnComponent);

    const notesHeader = headerOf(fixture, 'Notes');

    expect(notesHeader.classList.contains('p-datatable-sortable-column')).toBeFalse();
    expect(notesHeader.querySelector('p-sorticon')).toBeNull();
  });
});

const STALE_SORT_STATE_KEY = 'sdt-stale-sort-spec';

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
class HostWithPersistedCommaSeparatedSortComponent {
  cols: Column[] = [
    { name: 'Title', field: 'title', filterType: 'text' },
    { name: 'Sku', field: 'productVariantsCommaSeparated', filterType: 'text' },
  ];
  stateKey = STALE_SORT_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — persisted sort on a non-sortable column', () => {
  // Persisted state outlives the rule that produced it: a sort stored before the column became
  // non-sortable would ride every lazy load, and the backend answers an unknown sort field with a
  // 400 — unclearable from the UI, since the header that would clear it is no longer clickable.

  it('drops the stale sort instead of sending it', () => {
    sessionStorage.setItem(
      STALE_SORT_STATE_KEY,
      JSON.stringify({
        multiSortMeta: [{ field: 'productVariantsCommaSeparated', order: 1 }],
      }),
    );

    const fixture = createFixture(HostWithPersistedCommaSeparatedSortComponent);
    const host = fixture.componentInstance;

    expect(host.captured.length).toBe(1);
    expect(host.captured[0].multiSortMeta ?? null).toBeNull();
  });

  it('keeps a persisted sort on a sortable column', () => {
    sessionStorage.setItem(
      STALE_SORT_STATE_KEY,
      JSON.stringify({ multiSortMeta: [{ field: 'title', order: -1 }] }),
    );

    const fixture = createFixture(HostWithPersistedCommaSeparatedSortComponent);
    const host = fixture.componentInstance;

    expect(host.captured[0].multiSortMeta).toEqual([
      { field: 'title', order: -1 },
    ]);
  });
});

// ── per-column cell templates ────────────────────────────────────────────────
// The point of the slot is that a column opts in ALONE: everything not named by a template keeps
// rendering exactly as before, and the column's header, filter and sort are untouched by it.

const idAndNameCols: Column[] = [
  { name: 'Id', field: 'id', filterType: 'numeric' },
  { name: 'Name', field: 'name', filterType: 'text' },
];

// id is fractional and four digits so the FORMATTED value ("1,234.5" under the test locale) is
// distinguishable from the raw one (1234.5) — that is what tells the two context members apart.
const oneRow = (): Observable<PaginatedResult> =>
  paginated([{ id: 1234.5, name: 'Ana' }]);

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

async function renderRows(
  fixture: ComponentFixture<unknown>,
): Promise<HTMLElement> {
  await fixture.whenStable();
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
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
  // only filter surface, which is the one thing a data table must not trade for looks.
  it('leaves the templated column its header filter', async () => {
    const fixture = createFixture(HostWithCellTemplateComponent);
    const el = await renderRows(fixture);

    const headers = Array.from(el.querySelectorAll('thead th'));
    const idHeader = headers.find((th) => th.textContent!.includes('Id'))!;
    expect(idHeader.querySelector('p-columnfilter')).toBeTruthy();
  });
});

describe('SpiderlyDataTableComponent — Column.minWidth', () => {
  it('overrides the filter type default, and only when declared', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getColHeaderWidth({ filterType: 'numeric' })).toBe(
      'min-width: 12rem;',
    );
    expect(
      dataTable.getColHeaderWidth({ filterType: 'numeric', minWidth: '8rem' }),
    ).toBe('min-width: 8rem;');
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

const fourSelectableRows = (): Observable<PaginatedResult> =>
  paginated([{ id: 1 }, { id: 2 }, { id: 3 }, { id: 4 }]);

/** createWithDataTable + the settle-and-render ritual (renderRows), for suites that need rows. */
async function renderStable<T>(host: new () => T): Promise<{
  fixture: ComponentFixture<T>;
  host: T;
  dataTable: SpiderlyDataTableComponent;
}> {
  const created = createWithDataTable(host);
  await renderRows(created.fixture);
  return created;
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [selectionMode]="'multiple'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSelectionComponent {
  cols = cols;
  getList = fourSelectableRows;
}

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

/** Presses a row's selection checkbox the way a user does: mousedown on the cell, click on the input. */
function clickRowCheckbox(
  fixture: ComponentFixture<unknown>,
  rowIndex: number,
  { shift = false } = {},
): void {
  const el = fixture.nativeElement as HTMLElement;
  const cell = el.querySelectorAll('tbody td.selection-cell')[
    rowIndex
  ] as HTMLElement;
  const input = cell.querySelector('input[type="checkbox"]') as HTMLInputElement;
  input.dispatchEvent(
    new MouseEvent('mousedown', {
      bubbles: true,
      cancelable: true,
      shiftKey: shift,
    }),
  );
  input.click();
  fixture.detectChanges();
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

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [rows]="15"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithCustomRowsComponent {
  cols = cols;
  getList = emptyList;
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [rows]="pageSize"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithLateRowsComponent {
  cols = cols;
  pageSize: number | null = null;
  getList = emptyList;
}

describe('SpiderlyDataTableComponent — rows-per-page options', () => {
  it('offers the default page-size choices in the paginator dropdown', async () => {
    const { fixture, dataTable } = await renderStable(HostWithSelectionComponent);

    expect(dataTable.rowsPerPageOptions).toEqual([10, 25, 50, 100]);
    const dropdown = (fixture.nativeElement as HTMLElement).querySelector(
      'p-paginator p-select',
    );
    expect(dropdown)
      .withContext('paginator rows-per-page dropdown should render')
      .toBeTruthy();
  });

  it('merges a custom initial rows value into the options', () => {
    const { dataTable } = createWithDataTable(HostWithCustomRowsComponent);

    expect(dataTable.rowsPerPageOptions).toEqual([10, 15, 25, 50, 100]);
  });

  // PrimeNG's restoreState later overwrites `rows` with the persisted pick, so a stored value
  // missing from the options would blank the dropdown just like an unmerged custom `rows`.
  it('merges a persisted page-size pick into the options', () => {
    sessionStorage.setItem('spiderly-table:/', JSON.stringify({ rows: 33 }));

    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.rowsPerPageOptions).toEqual([10, 25, 33, 50, 100]);
  });

  // Storage is user-writable; an offered page size goes straight to the backend's uncapped Take.
  for (const rows of [100000, '33', 0, -5, 12.5]) {
    it(`refuses a persisted page size of ${JSON.stringify(rows)}`, () => {
      sessionStorage.setItem('spiderly-table:/', JSON.stringify({ rows }));

      const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

      expect(dataTable.rowsPerPageOptions).toEqual([10, 25, 50, 100]);
    });
  }

  // A consumer may resolve [rows] asynchronously (route data, a settings fetch); the merge has
  // to re-run or PrimeNG lands on a page size the dropdown cannot show.
  it('merges a rows value that arrives after init', () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithLateRowsComponent,
    );

    host.pageSize = 15;
    fixture.detectChanges();

    expect(dataTable.rowsPerPageOptions).toEqual([10, 15, 25, 50, 100]);
  });
});

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

describe('SpiderlyDataTableComponent — active-filter header icon', () => {
  // The projected filtericon template's element. PrimeNG wraps a projected template in
  // `span.pi-filter-icon`; without the projection it renders its own SVG <FilterIcon>,
  // so a null here means the template is missing entirely.
  const filterIcon = (el: HTMLElement): HTMLElement | null =>
    el.querySelector('th .p-datatable-column-filter-button i.pi');

  it('fills the icon while a constraint is active and unfills it after clear', () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithoutActionsComponent,
    );
    const el: HTMLElement = fixture.nativeElement;

    expect(filterIcon(el))
      .withContext('the filtericon template should render an .pi icon')
      .toBeTruthy();
    expect(filterIcon(el)!.classList).not.toContain('pi-filter-fill');

    dataTable.table.filters['id'] = [
      { value: 5, matchMode: 'equals', operator: 'and' },
    ];
    dataTable.table._filter();
    fixture.detectChanges();

    expect(filterIcon(el)!.classList)
      .withContext('a live constraint should fill the icon')
      .toContain('pi-filter-fill');

    dataTable.clear(dataTable.table);
    fixture.detectChanges();

    expect(filterIcon(el)!.classList)
      .withContext('clearing all filters should unfill the icon')
      .not.toContain('pi-filter-fill');
  });

  // Per-column icon lookup for multi-column hosts.
  const iconInHeader = (el: HTMLElement, name: string): HTMLElement | null => {
    const th = Array.from(el.querySelectorAll('th')).find((h) =>
      (h.textContent ?? '').includes(name),
    );
    return th?.querySelector('i.pi') ?? null;
  };

  // The worst case the icon exists for: stateStorage restores filters on reload with no
  // interaction. Karma runs dev mode, so this spec also guards the `#dt`-parameter timing —
  // an implementation reading the non-static `this.table` ViewChild would paint inactive
  // first and throw ExpressionChangedAfterItHasBeenCheckedError right here.
  it('marks a restored filter on first paint, with no interaction', () => {
    sessionStorage.setItem(
      COLUMNS_STATE_KEY,
      JSON.stringify({
        filters: { name: [{ value: 'abc', matchMode: 'contains' }] },
      }),
    );

    const el: HTMLElement = createFixture(HostWithColumnsStateKeyComponent)
      .nativeElement;

    expect(iconInHeader(el, 'Name')!.classList)
      .withContext('the restored constraint should fill its column icon')
      .toContain('pi-filter-fill');
    expect(iconInHeader(el, 'Id')!.classList)
      .withContext('an unfiltered column stays unfilled')
      .not.toContain('pi-filter-fill');
  });

  // Guards against "simplifying" onto the template's `let-hasFilter`: PrimeNG's getter
  // reads only the first constraint of array meta, so this state — first slot blanked,
  // second still constraining — is genuinely filtered while hasFilter reports it isn't.
  it('stays filled when only a later constraint of a multi-constraint column holds a value', () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithoutActionsComponent,
    );
    const el: HTMLElement = fixture.nativeElement;

    dataTable.table.filters['id'] = [
      { value: null, matchMode: 'equals', operator: 'and' },
      { value: 5, matchMode: 'equals', operator: 'and' },
    ];
    fixture.detectChanges();

    expect(filterIcon(el)!.classList)
      .withContext('a live second constraint should keep the icon filled')
      .toContain('pi-filter-fill');
  });
});

describe('SpiderlyDataTableComponent — filter menu Apply button', () => {
  @Component({
    imports: [SpiderlyDataTableComponent],
    template: `
      <spiderly-data-table
        [cols]="cols"
        [getPaginatedListObservableMethod]="getList"
      ></spiderly-data-table>
    `,
  })
  class HostWithBooleanColumnComponent {
    cols: Column[] = [
      { name: 'Name', field: 'name', filterType: 'text' },
      { name: 'Active', field: 'active', filterType: 'boolean' },
    ];
    getList = emptyList;
  }

  // Opens the named column's filter menu and returns its buttonbar's buttons.
  const buttonbarButtons = (
    fixture: ComponentFixture<unknown>,
    headerName: string,
  ): HTMLButtonElement[] => {
    const el: HTMLElement = fixture.nativeElement;
    const th = Array.from(el.querySelectorAll('th')).find((h) =>
      (h.textContent ?? '').includes(headerName),
    )!;
    th.querySelector<HTMLElement>('.p-datatable-column-filter-button')!.click();
    fixture.detectChanges();

    return Array.from(
      el.querySelectorAll<HTMLButtonElement>(
        '.p-datatable-filter-overlay .p-datatable-filter-buttonbar button',
      ),
    );
  };

  it('renders no Apply button for an auto-applying filter type', () => {
    const fixture = createFixture(HostWithBooleanColumnComponent);

    // Boolean applies on every checkbox change (PrimeNG's own onModelChange), so an
    // Apply button would promise a pending state that cannot exist. Clear stays — it is
    // the only way from checked/unchecked back to "no constraint".
    expect(buttonbarButtons(fixture, 'Active').length)
      .withContext('auto-applying menu should keep only the Clear button')
      .toBe(1);
  });

  it('keeps the Apply button for typed filter input', () => {
    const fixture = createFixture(HostWithBooleanColumnComponent);

    expect(buttonbarButtons(fixture, 'Name').length)
      .withContext('text filter commits on Enter/Apply, so Apply must stay')
      .toBe(2);
  });
});
