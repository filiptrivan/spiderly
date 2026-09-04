import { Component, ErrorHandler } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter, Router } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { DialogService } from 'primeng/dynamicdialog';
import { Popover } from 'primeng/popover';
import { Observable, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';

import { ColumnFilter } from 'primeng/table';

import {
  expectPendingVeil,
  paginated,
  renderRows,
  tbodyText,
  translocoTesting,
} from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Filter } from '../../entities/filter';
import { PaginatedResult } from '../../entities/paginated-result';
import { SpiderlyCellTemplateDirective } from '../../directives/spiderly-cell-template.directive';
import { SpiderlyDataTableActionsDirective } from '../../directives/spiderly-data-table-actions.directive';
import {
  createFilterStore,
  textFilter,
} from '../../filters/filter-store';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { ConfigServiceBase } from '../../services/config.service.base';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import {
  Column,
  SpiderlyDataTableComponent,
} from './spiderly-data-table.component';

const cols: Column[] = [{ name: 'Id', field: 'id', filterType: 'numeric' }];

const emptyList = (): Observable<PaginatedResult> => paginated([]);

// Snapshot each filter — PrimeNG mutates/reuses the lazy-load event object.
const capturingGetList =
  (captured: Filter[]) =>
  (filter: Filter): Observable<PaginatedResult> => {
    captured.push(JSON.parse(JSON.stringify(filter)));
    return emptyList();
  };

// The library's error owner, stubbed so a spec can assert a failure reached it. Reset with the
// stores below, since Jasmine spies persist across the specs in a file.
const errorHandler = { handleError: jasmine.createSpy('handleError') };

// Every suite touches persisted table state; wipe both stores between tests.
afterEach(() => {
  sessionStorage.clear();
  localStorage.clear();
  errorHandler.handleError.calls.reset();
});

function createFixture<T>(host: new () => T): ComponentFixture<T> {
  TestBed.configureTestingModule({
    imports: [
      host,
      TranslocoTestingModule.forRoot(
        translocoTesting({ ResultCount: '{{count}} results' }),
      ),
    ],
    providers: [
      provideNoopAnimations(),
      provideRouter([]),
      { provide: ConfigServiceBase, useValue: { defaultPageSize: 10 } },
      { provide: SpiderlyMessageService, useValue: {} },
      { provide: DialogService, useValue: {} },
      { provide: ErrorHandler, useValue: errorHandler },
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

function headerNamed(el: HTMLElement, name: string): HTMLTableCellElement {
  return headerCells(el).find((th) => (th.textContent ?? '').includes(name))!;
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

// There is more than one popover on this component now (the chooser and the header menu), so a
// container is found by what it HOLDS. Taking the first by declaration order silently followed
// whichever happened to be written first, which is how adding the header menu broke two
// clear-on-hide specs that had nothing to do with it.
function overlayHolding(
  fixture: ComponentFixture<unknown>,
  selector: string,
): HTMLElement | undefined {
  return fixture.debugElement
    .queryAll(By.directive(Popover))
    .map((debugEl) => (debugEl.componentInstance as Popover).container)
    .filter(Boolean)
    .find((container) => (container as HTMLElement).querySelector(selector)) as
    | HTMLElement
    | undefined;
}

function columnMenu(fixture: ComponentFixture<unknown>): HTMLElement {
  return overlayHolding(fixture, '[data-testid="column-menu-hide"]')!;
}

// Query THIS fixture's popover container — once open, PrimeNG appends it to
// document.body, where stale popovers from earlier fixtures may also linger.
function chooserContainer(
  fixture: ComponentFixture<unknown>,
): HTMLElement | undefined {
  return overlayHolding(fixture, '.column-chooser');
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
  ): HTMLTableCellElement => headerNamed(fixture.nativeElement, name);

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

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithRowActionsComponent {
  cols: Column[] = [
    { name: 'Name', field: 'name', filterType: 'text' },
    { actions: [{ field: 'Details' }, { field: 'Delete' }] },
  ];
  getList = () => paginated([{ id: 1, name: 'Ana' }]);
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
class HostWithMoreColumnsThanFitComponent {
  // Six text columns at the 12rem default = 72rem, well past the Karma fixture's width.
  cols: Column[] = Array.from({ length: 6 }, (_, i) => ({
    name: `Col ${i}`,
    field: `c${i}`,
    filterType: 'text' as const,
  }));
  getList = () => paginated([{ id: 1 }]);
}

describe('SpiderlyDataTableComponent — a table too wide for its container scrolls', () => {
  // The worry was that fixed layout sizes columns from the table's width, so a laptop would get
  // crushed columns where auto layout gave a horizontal scroll. It does not: a table whose
  // declared column widths exceed its container is widened to their sum by the browser, and the
  // wrapper's overflow-auto still scrolls. Measured 2026-08-29 — six 12rem columns rendered a
  // 1152px table inside a 740px container, each header exactly 192px. No `min-width` needed;
  // this test exists so that free property is not silently lost.
  it('keeps the table at least as wide as its columns need', async () => {
    const { fixture } = await renderStable(HostWithMoreColumnsThanFitComponent);
    const el = fixture.nativeElement as HTMLElement;

    const table = el.querySelector('table')!.getBoundingClientRect().width;
    const container = el
      .querySelector('.spiderly-table-container')!
      .getBoundingClientRect().width;

    expect(table).toBeGreaterThan(container);
  });
});

describe('SpiderlyDataTableComponent — columns carrying no value still get room', () => {
  // Both leaned on `width: 0rem` meaning "shrink to fit" — see CLAUDE.md → Column widths.
  it('leaves an actions column room for its icons', async () => {
    const { fixture } = await renderStable(HostWithRowActionsComponent);

    const headers = headerCells(fixture.nativeElement as HTMLElement);
    const actions = headers[headers.length - 1];
    expect(actions.getBoundingClientRect().width).toBeGreaterThan(0);
  });

  it('leaves the selection checkbox column a real width', async () => {
    const { fixture } = await renderStable(HostWithSelectionComponent);

    const [selection] = headerCells(fixture.nativeElement as HTMLElement);
    expect(selection.getBoundingClientRect().width).toBeGreaterThan(0);
  });
});

describe('SpiderlyDataTableComponent — Column.width', () => {
  it('overrides the filter type default, and only when declared', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getColWidth({ filterType: 'numeric' })).toBe('12rem');
    expect(dataTable.getColWidth({ filterType: 'text', width: '4rem' })).toBe(
      '4rem',
    );
  });

  // Every filterType needs a row in the width table, or it silently inherits the actions-column
  // reservation — which is what the switch this replaced did to `blob`.
  it('sizes a blob column for its thumbnail, not as an actions column', () => {
    const { dataTable } = createWithDataTable(HostWithoutActionsComponent);

    expect(dataTable.getColWidth({ filterType: 'blob' })).toBe('5rem');
  });
});

// Two values whose only difference is how much room the text WANTS. Under the browser's default
// `table-layout: auto` that difference is what sized the columns.
const SHORT_NAME = 'Ana';
const LONG_NAME = 'Aleksandar Konstantinović-Petrović, Sremska Kamenica';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSwappableRowsComponent {
  cols = idAndNameCols;
  rowName = SHORT_NAME;
  getList = () => paginated([{ id: 1, name: this.rowName }]);
}

/**
 * Re-renders the single row with different text, which is what every claim in these suites rests
 * on. Two things a caller must not lose: the reload runs INSIDE the fixture's zone, as a real
 * Reload click would — called straight from a test body it schedules its `delay(0)` outside, so
 * `whenStable` returns before the rows land — and the swap is asserted to have landed, because
 * the first revision of these tests measured a still-loading table twice and passed.
 */
async function swapRowNameTo(
  created: {
    fixture: ComponentFixture<HostWithSwappableRowsComponent>;
    host: HostWithSwappableRowsComponent;
    dataTable: SpiderlyDataTableComponent;
  },
  value: string,
): Promise<void> {
  created.host.rowName = value;
  created.fixture.ngZone!.run(() => created.dataTable.reload());
  const el = await renderRows(created.fixture);
  expect(el.querySelector('tbody')!.textContent).toContain(value.trim());
}

/** As RENDERED — the geometry the operator's eye tracks, not the style string we hand PrimeNG. */
function headerWidths(el: HTMLElement): number[] {
  return headerCells(el).map((th) => th.getBoundingClientRect().width);
}

describe('SpiderlyDataTableComponent — column widths ignore the content', () => {
  it('keeps every column the same width when the rows change', async () => {
    const created = await renderStable(HostWithSwappableRowsComponent);
    const el = created.fixture.nativeElement as HTMLElement;
    const before = headerWidths(el);

    await swapRowNameTo(created, LONG_NAME);

    expect(headerWidths(el)).toEqual(before);
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
class HostWithMixedFilterTypesComponent {
  cols: Column[] = [
    { name: 'Name', field: 'name', filterType: 'text' },
    { name: 'Active', field: 'active', filterType: 'boolean' },
  ];
  // No rows on purpose: the widths under test come from the declarations, not the content.
  getList = emptyList;
}

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

describe('SpiderlyDataTableComponent — column widths follow the declared filter type', () => {
  // Fixed layout splits the table into EQUAL shares for columns that declare no width, which
  // would throw away what the per-filterType defaults say: a boolean holds "Da"/"Ne" and a text
  // column holds a name. Same proportions as before, just no longer moved by the content.
  it('gives a boolean column less room than a text column', async () => {
    const { fixture } = createWithDataTable(HostWithMixedFilterTypesComponent);
    const el = await renderRows(fixture);

    // The surplus is shared in PROPORTION to the declared widths, so the ratio survives whatever
    // width the table ends up with — measured 12rem/8rem = 1.5 exactly. That is what makes these
    // defaults carry the same intent under fixed layout as the minimums did under auto.
    const [text, boolean] = headerWidths(el);
    expect(text / boolean).toBeCloseTo(12 / 8, 2);
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

// The filter menu button PrimeNG renders in each filterable column's header.
const FILTER_BUTTON = '.p-datatable-column-filter-button';

// The projected filtericon template's element in the named column's header. PrimeNG
// wraps a projected template in `span.pi-filter-icon`; without the projection it renders
// its own SVG <FilterIcon>, so a null here means the template is missing entirely.
const filterIcon = (el: HTMLElement, headerName: string): HTMLElement | null =>
  headerNamed(el, headerName).querySelector<HTMLElement>(
    `${FILTER_BUTTON} i.pi`,
  );

describe('SpiderlyDataTableComponent — active-filter header icon', () => {
  it('leaves the icon unfilled while a value is typed but not yet applied', () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithoutActionsComponent,
    );
    const el: HTMLElement = fixture.nativeElement;

    // Exactly what PrimeNG does on each keystroke in a text/numeric filter: onModelChange
    // writes the value straight into the table's filter meta and calls _filter() ONLY for
    // the auto-applying types. So the constraint sits there, pending, until Apply/Enter —
    // and the icon has to describe the DATA on screen, not the edit in progress.
    dataTable.table.filters['id'] = [
      { value: 5, matchMode: 'equals', operator: 'and' },
    ];
    fixture.detectChanges();

    expect(filterIcon(el, 'Id')!.classList)
      .withContext('a typed but unapplied value must not mark the column filtered')
      .not.toContain('pi-filter-fill');

    dataTable.table._filter();
    fixture.detectChanges();

    expect(filterIcon(el, 'Id')!.classList)
      .withContext('applying it is what fills the icon')
      .toContain('pi-filter-fill');
  });

  // The same claim as the spec above, but driven through the REAL widget instead of by
  // hand-writing `table.filters`. That distinction is the whole point: the hand-written
  // version encodes what we BELIEVE PrimeNG does on a keystroke, so it cannot catch us
  // believing wrong — it passed while the live admin still filled the icon mid-typing.
  it('leaves the icon unfilled while the operator types into the real filter input', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithColumnsStateKeyComponent,
    );
    const el: HTMLElement = fixture.nativeElement;

    headerNamed(el, 'Name')
      .querySelector<HTMLElement>(FILTER_BUTTON)!
      .click();
    fixture.detectChanges();
    await fixture.whenStable();

    const columnFilter = fixture.debugElement
      .queryAll(By.directive(ColumnFilter))
      .map((de) => de.componentInstance as ColumnFilter)
      .find((cf) => cf.overlayVisible)!;
    const overlay = columnFilter.overlay as HTMLElement;

    const input = overlay.querySelector<HTMLInputElement>('input');
    expect(input).withContext('the text filter renders an input').toBeTruthy();

    input!.value = 'bosch';
    input!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    // Negative control: prove the keystroke actually landed in the table's filter meta.
    // Without this the spec would also pass when the input event never reached ngModel —
    // an unfilled icon for the wrong reason, i.e. a test that inspected nothing.
    expect((dataTable.table.filters['name'] as any[])?.[0]?.value)
      .withContext('the typed value reaches the meta — this is the state that used to fill the icon')
      .toBe('bosch');

    expect(filterIcon(el, 'Name')!.classList)
      .withContext('typing alone must not mark the column filtered')
      .not.toContain('pi-filter-fill');

    // Apply is what commits it — the same button the operator presses.
    const apply = Array.from(
      overlay.querySelectorAll<HTMLButtonElement>(
        '.p-datatable-filter-buttonbar button',
      ),
    ).find((b) => !(b.textContent ?? '').toLowerCase().includes('clear'))!;
    apply.click();
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(filterIcon(el, 'Name')!.classList)
      .withContext('applying it fills the icon')
      .toContain('pi-filter-fill');
    expect(dataTable.table.filters['name'])
      .withContext('and the constraint really did reach the table')
      .toBeTruthy();
  });

  it('fills the icon while a constraint is active and unfills it after clear', () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithoutActionsComponent,
    );
    const el: HTMLElement = fixture.nativeElement;

    expect(filterIcon(el, 'Id'))
      .withContext('the filtericon template should render an .pi icon')
      .toBeTruthy();
    expect(filterIcon(el, 'Id')!.classList).not.toContain('pi-filter-fill');

    dataTable.table.filters['id'] = [
      { value: 5, matchMode: 'equals', operator: 'and' },
    ];
    dataTable.table._filter();
    fixture.detectChanges();

    expect(filterIcon(el, 'Id')!.classList)
      .withContext('an applied constraint should fill the icon')
      .toContain('pi-filter-fill');

    dataTable.clear(dataTable.table);
    fixture.detectChanges();

    expect(filterIcon(el, 'Id')!.classList)
      .withContext('clearing all filters should unfill the icon')
      .not.toContain('pi-filter-fill');
  });

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

    expect(filterIcon(el, 'Name')!.classList)
      .withContext('the restored constraint should fill its column icon')
      .toContain('pi-filter-fill');
    expect(filterIcon(el, 'Id')!.classList)
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
    dataTable.table._filter();
    fixture.detectChanges();

    expect(filterIcon(el, 'Id')!.classList)
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

  // Opens the named column's filter menu and returns its buttonbar's button labels.
  //
  // Async, and read through the ColumnFilter's own `overlay`, for one reason: PrimeNG
  // assigns `overlay` and teleports it to document.body from its animation-start callback,
  // which under provideNoopAnimations runs in a MICROTASK after detectChanges() returns.
  // So synchronously the panel is still inline and `overlay` is undefined — a fixture-root
  // query would pass today and silently return [] the moment anything awaited stability.
  // Awaiting first is what the component CLAUDE.md prescribes for overlay specs, and
  // querying the instance's own element (never document) keeps stale popovers from earlier
  // fixtures out of the result.
  const buttonbarLabels = async (
    fixture: ComponentFixture<unknown>,
    headerName: string,
  ): Promise<string[]> => {
    headerNamed(fixture.nativeElement, headerName)
      .querySelector<HTMLElement>(FILTER_BUTTON)!
      .click();
    fixture.detectChanges();
    await fixture.whenStable();

    const columnFilter = fixture.debugElement
      .queryAll(By.directive(ColumnFilter))
      .map((de) => de.componentInstance as ColumnFilter)
      .find((cf) => cf.overlayVisible)!;

    return Array.from(
      (columnFilter.overlay as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '.p-datatable-filter-buttonbar button',
      ),
    ).map((button) => button.textContent?.trim() ?? '');
  };

  it('renders no Apply button for an auto-applying filter type', async () => {
    const fixture = createFixture(HostWithBooleanColumnComponent);

    // Boolean applies on every checkbox change (PrimeNG's own onModelChange), so an Apply
    // button would promise a pending state that cannot exist. Asserting WHICH button
    // survives, not how many: Clear is the only way from checked/unchecked back to "no
    // constraint", so a change that dropped Clear instead of Apply must not stay green.
    const labels = (await buttonbarLabels(fixture, 'Active')).map((l) =>
      l.toLowerCase(),
    );

    expect(labels.length).toBe(1);
    expect(labels[0])
      .withContext('the surviving button is Clear, not Apply')
      .toContain('clear');
  });

  it('keeps the Apply button for typed filter input', async () => {
    const fixture = createFixture(HostWithBooleanColumnComponent);

    const labels = (await buttonbarLabels(fixture, 'Name')).map((l) =>
      l.toLowerCase(),
    );

    expect(labels.some((l) => l.includes('apply')))
      .withContext('text filter commits on Enter/Apply, so Apply must stay')
      .toBeTrue();
    expect(labels.some((l) => l.includes('clear')))
      .withContext('and Clear stays alongside it')
      .toBeTrue();
  });
});

describe('SpiderlyDataTableComponent — Column.matchModes narrowing', () => {
  @Component({
    imports: [SpiderlyDataTableComponent],
    template: `
      <spiderly-data-table
        [cols]="cols"
        [getPaginatedListObservableMethod]="getList"
      ></spiderly-data-table>
    `,
  })
  class HostWithDateColumnsComponent {
    cols: Column[] = [
      {
        name: 'CreatedAt',
        field: 'createdAt',
        filterType: 'date',
        showMatchModes: true,
        matchModes: [MatchModeCodes.GreaterThan, MatchModeCodes.LessThan],
      },
      { name: 'PaidAt', field: 'paidAt', filterType: 'date', showMatchModes: true },
    ];
    getList = emptyList;
  }

  it('offers only the declared match modes, in declared order, defaulting to the first', () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithDateColumnsComponent,
    );

    const columnFilters = fixture.debugElement
      .queryAll(By.directive(ColumnFilter))
      .map((de) => de.componentInstance as ColumnFilter);
    const narrowed = columnFilters.find((cf) => cf.field === 'createdAt')!;
    const untouched = columnFilters.find((cf) => cf.field === 'paidAt')!;

    // Assert `matchModes` (what PrimeNG RENDERS), never `matchModeOptions` (the input we
    // just handed in): PrimeNG resolves `matchModeOptions || <its own type defaults>`, so
    // an assertion on the input cannot tell a real narrowing from a silent fallback to
    // PrimeNG's list — which is exactly how a regression here would hide.
    expect(narrowed.matchModes!.map((o) => o.value))
      .withContext('declared modes only, in declared order')
      .toEqual([MatchModeCodes.GreaterThan, MatchModeCodes.LessThan]);
    // PrimeNG initializes the field's constraint from the matchMode input, so the
    // table's own filter model is where the column default is observable.
    expect(
      (dataTable.table.filters['createdAt'] as any[])[0].matchMode,
    )
      .withContext('the first declared mode is the column default')
      .toBe(MatchModeCodes.GreaterThan);

    expect(untouched.matchModes!.map((o) => o.value))
      .withContext("a column without matchModes keeps the library's own list")
      .toEqual([
        MatchModeCodes.Equals,
        MatchModeCodes.LessThan,
        MatchModeCodes.GreaterThan,
      ]);
    expect((dataTable.table.filters['paidAt'] as any[])[0].matchMode)
      .withContext('and the filter type standard default')
      .toBe(MatchModeCodes.Equals);
  });

  it('falls back to the full list, loudly, when no declared mode is supported', () => {
    @Component({
      imports: [SpiderlyDataTableComponent],
      template: `
        <spiderly-data-table
          [cols]="cols"
          [getPaginatedListObservableMethod]="getList"
        ></spiderly-data-table>
      `,
    })
    class HostWithImpossibleMatchModesComponent {
      cols: Column[] = [
        {
          name: 'Qty',
          field: 'qty',
          filterType: 'numeric',
          showMatchModes: true,
          matchModes: [MatchModeCodes.In],
        },
      ];
      getList = emptyList;
    }

    spyOn(console, 'error');
    const { fixture, dataTable } = createWithDataTable(
      HostWithImpossibleMatchModesComponent,
    );

    const columnFilter = fixture.debugElement.query(By.directive(ColumnFilter))
      .componentInstance as ColumnFilter;

    // An empty array is TRUTHY in PrimeNG's `matchModeOptions || defaults`, so a narrowing
    // that filtered everything out would render a dropdown with no options while the
    // unsupported mode still seeded the constraint (and 400'd server-side).
    expect(columnFilter.matchModes!.length)
      .withContext('an unusable narrowing keeps the full list rather than emptying it')
      .toBe(3);
    expect((dataTable.table.filters['qty'] as any[])[0].matchMode)
      .withContext('and the default stays the filter type standard')
      .toBe(MatchModeCodes.Equals);
    expect(console.error).toHaveBeenCalled();
  });

  it('repairs a persisted match mode the column no longer offers', () => {
    sessionStorage.setItem(
      COLUMNS_STATE_KEY,
      JSON.stringify({
        filters: {
          createdAt: [{ value: '2026-01-01', matchMode: MatchModeCodes.Equals }],
        },
      }),
    );

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
    class HostWithNarrowedDateComponent {
      cols: Column[] = [
        {
          name: 'CreatedAt',
          field: 'createdAt',
          filterType: 'date',
          showMatchModes: true,
          matchModes: [MatchModeCodes.GreaterThan, MatchModeCodes.LessThan],
        },
      ];
      stateKey = COLUMNS_STATE_KEY;
      getList = emptyList;
    }

    const { dataTable } = createWithDataTable(HostWithNarrowedDateComponent);

    // ColumnFilter.ngOnInit skips initFieldFilterConstraint() when the field already has a
    // constraint, so [matchMode] never applies to a restored one — without the repair the
    // column keeps filtering by `equals` while its <p-select> renders blank.
    expect((dataTable.table.filters['createdAt'] as any[])[0].matchMode)
      .withContext('a stored mode outside the narrowing is reset to the column default')
      .toBe(MatchModeCodes.GreaterThan);
    expect((dataTable.table.filters['createdAt'] as any[])[0].value)
      .withContext('the value itself survives the repair')
      .toBe('2026-01-01');
  });
});

// A table with more records than fit on one page, so the paginator's Next is live.
const oneOfManyPages = (): Observable<PaginatedResult> => paginated([{ id: 1 }], 100);

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithManyPagesComponent {
  cols = cols;
  getList = oneOfManyPages;
}

// Shared by the scroll and pending-state suites — a real click, so the refetch it triggers is
// scheduled inside the fixture's zone and whenStable will wait for it.
function clickNextPage(root: HTMLElement): void {
  root.querySelector<HTMLElement>('.p-paginator-next')!.click();
}

describe('SpiderlyDataTableComponent — scroll on page change', () => {
  async function renderPaged() {
    const created = await renderStable(HostWithManyPagesComponent);
    const root = created.fixture.nativeElement as HTMLElement;
    const container = root.querySelector<HTMLElement>(
      '.spiderly-table-container',
    )!;
    return { ...created, root, container };
  }

  // The container's own geometry decides whether we scroll, and a Karma fixture is a small
  // box inside a page that never scrolls — so the position is stubbed, not arranged.
  function positionTop(el: HTMLElement, top: number): void {
    spyOn(el, 'getBoundingClientRect').and.returnValue({ top } as DOMRect);
  }

  // A details page can hold a table below a form (UIControlTypeCodes.Table). Scrolling
  // unconditionally would yank that form off-screen on every page flip.
  it('leaves the viewport alone when the table top is already in view', async () => {
    const { root, container } = await renderPaged();
    positionTop(container, 120);
    const scrolled = spyOn(container, 'scrollIntoView');

    clickNextPage(root);

    expect(scrolled).not.toHaveBeenCalled();
  });

  // Asserted as the whole options object, because `behavior` is the half that silently
  // rots: why 'instant' and not 'auto' is on scrollElementIntoViewIfAboveViewport.
  it('scrolls the table top back under the viewport, instantly', async () => {
    const { root, container } = await renderPaged();
    positionTop(container, -400);
    const scrolled = spyOn(container, 'scrollIntoView');

    clickNextPage(root);

    expect(scrolled).toHaveBeenCalledWith({
      block: 'start',
      inline: 'nearest',
      behavior: 'instant',
    });
  });

  // PrimeNG's restoreState assigns `first` directly and emits only firstChange — it never
  // routes through the paginator's onPageChange. A scroll on first paint would shove a
  // details page's form off-screen the moment the page opens.
  it('does not scroll on the first render, even with a restored page offset', async () => {
    sessionStorage.setItem(
      'spiderly-table:/',
      JSON.stringify({ first: 30, rows: 10 }),
    );
    const scrolled = spyOn(Element.prototype, 'scrollIntoView');

    const { fixture, dataTable } = await renderStable(
      HostWithManyPagesComponent,
    );
    const root = fixture.nativeElement as HTMLElement;

    expect(dataTable.table.first)
      .withContext('the restored offset did apply')
      .toBe(30);
    expect(scrolled).not.toHaveBeenCalled();

    // The spy is live, so the assertion above means something: the same element does
    // reach it once a real page change happens.
    const container = root.querySelector<HTMLElement>(
      '.spiderly-table-container',
    )!;
    positionTop(container, -400);
    clickNextPage(root);

    expect(scrolled).toHaveBeenCalled();
  });

  // Pins the seam described in Angular/CLAUDE.md → fixed-chrome offset: the component reads
  // the inset from a var it does not own, and still works when no shell declares one.
  it('offsets the scroll target by the shell viewport-top inset', () => {
    const { fixture } = createWithDataTable(HostWithoutActionsComponent);
    const container = (
      fixture.nativeElement as HTMLElement
    ).querySelector<HTMLElement>('.spiderly-table-container')!;

    expect(getComputedStyle(container).scrollMarginTop)
      .withContext('without the layout stylesheet: breathing room only')
      .toBe('16px');

    container.style.setProperty('--spiderly-viewport-top-inset', '80px');

    expect(getComputedStyle(container).scrollMarginTop)
      .withContext('a shell that declares its top inset is cleared')
      .toBe('96px');
  });
});

// lazyLoad's `next` is async and awaits the selected-ids call BEFORE lowering `loading`. A
// rejection there leaves that promise unsettled, and the subscriber's `error:` belongs to the
// paginated-list observable, so it never runs either — the table stays masked forever.
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
class HostWithFailingSelectionComponent {
  cols = cols;
  getList = () => paginated([{ id: 1 }]);
  getSelected = () => throwError(() => new Error('selected ids unavailable'));
}

// Each fetch answers with a different row, so "did the old page stay?" is answerable.
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithChangingPagesComponent {
  cols = cols;
  private page = 0;
  getList = () => paginated([{ id: ++this.page }], 100);
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
class HostWithFailingListComponent {
  cols = cols;
  getList = () => throwError(() => new Error('list unavailable'));
}

// Records the order the two fetches are issued in, before either can resolve.
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
class HostRecordingFetchOrderComponent {
  cols = cols;
  issued: string[] = [];
  getList = () => {
    this.issued.push('list');
    return paginated([{ id: 1 }]);
  };
  getSelected = () => {
    this.issued.push('selected');
    return of({ selectedIds: [], totalRecordsSelected: 0 } as any).pipe(
      delay(0),
    );
  };
}

describe('SpiderlyDataTableComponent — pending state', () => {
  // PrimeNG gates the empty message on `dt.isEmpty() && !dt.loading`. With the flag never
  // raised on a refetch, a table whose last result was empty answers "no records" for the
  // whole of the next request — asserting something it cannot know yet — and then flips to
  // rows. Driven through _filter() rather than reload(), which already raises the flag and
  // would make this pass for the wrong reason.
  it('does not claim there are no records while a refetch is in flight', async () => {
    const { fixture, dataTable } = await renderStable(HostWithoutActionsComponent);

    expect(tbodyText(fixture))
      .withContext('a settled empty result does say so')
      .toContain('NoRecordsFound');

    // Inside the zone, as a real filter commit is — driven from outside it the refetch's
    // delay(0) is scheduled where whenStable will not wait for it (the trap recorded on
    // swapRowNameTo, which is why this spec measured a still-pending table on first write).
    fixture.ngZone!.run(() => dataTable.table._filter());
    fixture.detectChanges();

    expect(tbodyText(fixture))
      .withContext('mid-flight it must not answer for data it does not have')
      .not.toContain('NoRecordsFound');

    await renderRows(fixture);

    expect(tbodyText(fixture))
      .withContext('and it says so again once the new result lands')
      .toContain('NoRecordsFound');
  });

  // PrimeNG's overlay carries no aria-busy and its spinner is aria-hidden, so without this the
  // pending state is purely visual and a screen reader is told nothing at all.
  it('marks the container busy, and veiled, while a refetch is in flight', async () => {
    const { fixture, dataTable } = await renderStable(HostWithoutActionsComponent);
    const root = fixture.nativeElement as HTMLElement;
    const busy = (): string | null =>
      root
        .querySelector('.spiderly-table-container')!
        .getAttribute('aria-busy');
    const mask = (): Element | null => root.querySelector('.p-datatable-mask');

    expect(busy()).withContext('settled').toBe('false');
    expect(mask()).withContext('settled').toBeNull();

    fixture.ngZone!.run(() => dataTable.table._filter());
    fixture.detectChanges();

    expect(busy()).withContext('in flight').toBe('true');
    // Same predicate through PrimeNG's binding — the visible half of the same fact, and the
    // whole user-visible complaint: a refetch reaches the overlay, not just the first load.
    expect(mask()).withContext('overlay in flight').not.toBeNull();

    await renderRows(fixture);

    expect(busy()).withContext('settled again').toBe('false');
    expect(mask()).withContext('overlay gone').toBeNull();
  });

  // Why lazyLoad raises the flag but never touches `items`: the previous page stays readable
  // under the overlay instead of the table blanking. Characterisation pin for that shape.
  it('keeps the previous rows on screen while the next page is fetched', async () => {
    const { fixture } = await renderStable(HostWithChangingPagesComponent);
    const root = fixture.nativeElement as HTMLElement;

    expect(tbodyText(fixture)).withContext('page one').toContain('1');

    clickNextPage(root);
    fixture.detectChanges();

    expect(tbodyText(fixture))
      .withContext('page one is still what the reader can see')
      .toContain('1');
    // And nothing else: PrimeNG renders a loadingbody template IN ADDITION to the rows, never
    // instead of them, so this is what keeps a "Loading..." row from wedging under the page.
    expect(root.querySelectorAll('tbody tr').length)
      .withContext('exactly the data rows')
      .toBe(1);

    await renderRows(fixture);

    expect(tbodyText(fixture)).withContext('page two lands').toContain('2');
  });

  // reload() blanked the table by nulling `items`, which is now the only refetch that does
  // not keep its rows. The null never drove the overlay anyway — the pending predicate tests
  // `items === undefined`, and null is not undefined — so it only ever drove isEmpty().
  it('keeps the current rows on screen while reload refetches', async () => {
    const created = await renderStable(HostWithChangingPagesComponent);
    const { fixture } = created;

    expect(tbodyText(fixture)).withContext('the loaded page').toContain('1');

    // Inside the zone, as the Reload button is — see swapRowNameTo.
    fixture.ngZone!.run(() => created.dataTable.reload());
    fixture.detectChanges();

    expect(tbodyText(fixture))
      .withContext('still readable under the veil')
      .toContain('1');

    await renderRows(fixture);

    expect(tbodyText(fixture)).withContext('the fresh page lands').toContain('2');
  });

  it('lowers the pending state even when the selected-ids call fails', async () => {
    const { fixture } = await renderStable(HostWithFailingSelectionComponent);

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.p-datatable-mask'),
    )
      .withContext('a failed selection fetch must not strand the overlay')
      .toBeNull();
    // Routed to the library's ErrorHandler rather than console: that is what toasts, skips
    // HttpErrorResponse (the interceptor already reported those), and reaches a consumer's
    // tracker through their own ErrorHandler wrapper.
    expect(errorHandler.handleError)
      .withContext('the failure still reaches an owner')
      .toHaveBeenCalled();
  });

  // The overlay is only honest if a FAILED load clears it too. isPending used to also test
  // `items === undefined`, which on a first-load failure stayed true forever.
  it('clears the pending state when the first load itself fails', async () => {
    const { fixture } = await renderStable(HostWithFailingListComponent);

    expect(
      (fixture.nativeElement as HTMLElement).querySelector('.p-datatable-mask'),
    )
      .withContext('a failed first load must not strand the overlay either')
      .toBeNull();
  });

  // The selected-ids call needs only the filter, so waiting for the list response cost a
  // second round trip on every page — and the overlay is now up for both.
  it('issues the selected-ids request alongside the list request, not behind it', () => {
    const { host } = createWithDataTable(HostRecordingFetchOrderComponent);

    expect(host.issued)
      .withContext('both in flight before either response lands')
      .toEqual(['selected', 'list']);
  });

  it('ignores reload() before anything has been loaded', () => {
    const { dataTable, host } = createWithDataTable(HostWithDefaultSortComponent);
    const loadsSoFar = host.captured.length;
    dataTable.lastLazyLoadEvent = null;

    expect(() => dataTable.reload()).not.toThrow();
    expect(host.captured.length)
      .withContext('nothing replayed — the initial load is already in flight')
      .toBe(loadsSoFar);
  });
});

describe('SpiderlyDataTableComponent — pending overlay styling', () => {
  // The ::ng-deep computed-style assert Angular/CLAUDE.md requires for any rule targeting
  // markup this component does not author. The pin table is shared with the data view's suite,
  // because the declaration is: table-pending-veil() in styles/layout/_mixins.scss.
  it('keeps the veil rules matching PrimeNG mask markup', () => {
    // Un-awaited on purpose: the fixture returns with the first fetch still pending, so the
    // mask is mounted. Same trick the scroll suite's scroll-margin pin uses.
    const fixture = createFixture(HostWithoutActionsComponent);

    expectPendingVeil(
      (fixture.nativeElement as HTMLElement).querySelector<HTMLElement>(
        '.p-datatable-mask',
      ),
    );
  });
});

// ---------------------------------------------------------------------------
// The filter surface is chosen by the SHAPE of the input, not by a flag. A table handed a filter
// store renders the chip bar; a table handed none keeps its Column.filterType header filters, so
// the 27 consumer tables migrate one at a time and the legacy path deletes itself once nothing
// passes the old shape. See spiderly-data-table/CLAUDE.md -> "Operator-owned view", decision 2.
// ---------------------------------------------------------------------------

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
class HostWithFilterStoreComponent {
  cols = cols;
  getList = emptyList;
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — the filter surface follows the input', () => {
  it('renders the chip bar and drops the header filters when given a store', () => {
    const { fixture } = createWithDataTable(HostWithFilterStoreComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).not.toBeNull();
    expect(fixture.debugElement.queryAll(By.directive(ColumnFilter)).length).toBe(
      0,
    );
  });

  it('keeps the header filters and draws no bar when given none', () => {
    const { fixture } = createWithDataTable(HostWithoutActionsComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).toBeNull();
    expect(
      fixture.debugElement.queryAll(By.directive(ColumnFilter)).length,
    ).toBeGreaterThan(0);
  });
});

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [defaultSortField]="'name'"
      [defaultSortOrder]="-1"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSortedFilterStoreComponent {
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Id', field: 'id', filterType: 'numeric' },
  ];
  getList = emptyList;
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
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
class HostWithCountingFilterStoreComponent {
  cols = cols;
  getList = (): Observable<PaginatedResult> => paginated([{ id: 1 }, { id: 2 }], 812);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
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
class HostWithCapturingFilterStoreComponent {
  cols = cols;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — a committed filter re-queries', () => {
  it('sends the store payload and asks the server again, from page one', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before + 1);
    expect(host.captured.at(-1)!.filters).toEqual({
      companyName: [
        { matchMode: MatchModeCodes.Contains, value: 'Elektromont' },
      ],
    } as any);
    // Narrowing from page 3 must not keep asking the server to skip rows a smaller result set
    // does not have — the failure is an empty grid for a value that is definitely there.
    expect(host.captured.at(-1)!.first).toBe(0);
  });

  // A draft is not a query: re-fetching on every keystroke is the other half of the lie the chip
  // bar exists to prevent, paid for in requests instead of in credibility.
  it('does not re-query for a draft', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elek',
      });
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before);
  });

  // A stray x on a filter nobody applied, or a commit of an unchanged draft, must not spend a
  // request. The effect watches `applied()`, and a freshly built Map is a new identity even when
  // its contents are the same, so without a guard every such gesture re-queries.
  it('does not re-query when a commit or reset changes nothing', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    const before = host.captured.length;

    await fixture.ngZone!.run(async () => {
      host.filters.commit('companyName');
      host.filters.reset('companyName');
      host.filters.clear();
    });
    await renderRows(fixture);

    expect(host.captured.length).toBe(before);
  });

  // An empty grid under a filter is otherwise a mystery: the chips say what was asked, and this
  // says what came back. It is the CURRENT query's count only — the unfiltered total would cost a
  // second request the backend does not offer.
  it('shows how many rows the current query returned', async () => {
    const { fixture } = createWithDataTable(HostWithCountingFilterStoreComponent);
    await renderRows(fixture);

    expect(
      (fixture.nativeElement as HTMLElement).querySelector(
        '[data-testid="filter-bar-count"]',
      )!.textContent,
    ).toContain('812');
  });

  // Multi-sort is invisible today: the only sign is sort icons scattered across headers, and
  // nothing says which of them is the primary key. The chip answers "why is the list in this
  // order" in one place, using the COLUMN NAME rather than the field an operator never sees.
  it('shows what the grid is sorted by, by name and direction', async () => {
    const { fixture } = createWithDataTable(HostWithSortedFilterStoreComponent);
    await renderRows(fixture);

    const sortChip = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="sort-chip"]',
    );

    expect(sortChip).not.toBeNull();
    expect(sortChip!.textContent).toContain('Naziv');
    expect(sortChip!.textContent).toContain('↓');
    expect(sortChip!.textContent).not.toContain('name');
  });

  // Two buttons for one job is the complaint. With a store supplied the toolbar's "Clear filters"
  // sits a row away from the chips it clears, so it goes and the bar owns the gesture — and the
  // bar's one has to do everything the toolbar's did, persisted state included.
  it('clears from the bar, and the toolbar no longer offers a second way', async () => {
    const { fixture, host } = createWithDataTable(
      HostWithCapturingFilterStoreComponent,
    );
    await renderRows(fixture);

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    const el = fixture.nativeElement as HTMLElement;
    // Scoped to the TOOLBAR: the bar's own clear button carries the same icon, deliberately (one
    // meaning, one glyph), so a bare icon query stopped meaning "the toolbar button is gone".
    expect(el.querySelector('.table-header .pi-filter-slash')).toBeNull();

    await fixture.ngZone!.run(async () => {
      el.querySelector<HTMLButtonElement>(
        '[data-testid="filter-bar-clear"]',
      )!.click();
    });
    await renderRows(fixture);

    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });

  // A bare digit beside a chip reads as a badge on it. The number needs a word.
  it('labels the result count instead of printing a bare number', async () => {
    const { fixture } = createWithDataTable(HostWithCountingFilterStoreComponent);
    await renderRows(fixture);

    const count = (fixture.nativeElement as HTMLElement).querySelector(
      '[data-testid="filter-bar-count"]',
    )!;

    expect(count.textContent!.trim()).not.toBe('812');
    expect(count.textContent).toContain('812');
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
class HostWithTwoColumnsComponent {
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Id', field: 'id', filterType: 'numeric' },
  ];
  getList = (): Observable<PaginatedResult> =>
    paginated([{ id: 1, name: 'kupac javio da nije kod kuce do petka' }]);
}

async function openColumnMenu(
  fixture: ComponentFixture<unknown>,
  index = 0,
): Promise<void> {
  (fixture.nativeElement as HTMLElement)
    .querySelectorAll<HTMLButtonElement>('[data-testid="column-menu"]')[index]
    .click();
  fixture.detectChanges();
  await fixture.whenStable();
  fixture.detectChanges();
}

// Hiding a column meant opening the chooser popover and hunting for its row. The column itself is
// where the gesture belongs, and the menu is the surface the width, order and wrap controls will
// share (CLAUDE.md -> "Operator-owned view", decision 3).
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
  // way (CLAUDE.md -> decision 9), but WHICH column gives up its row height is the operator's
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

// The safety net for all four gestures. Until the header menu arrived, "reset to default" only
// had visibility to undo; now an operator can also wrap, move and resize, and a layout with no
// way back is worse than one with no knobs.
describe('SpiderlyDataTableComponent — resetting the layout', () => {
  it('undoes wrap, order and width, not just visibility', async () => {
    const { fixture, dataTable } = createWithDataTable(
      HostWithTwoColumnsComponent,
    );
    await renderRows(fixture);

    const declared = dataTable.cols.map((col) => col.field);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-right"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).not.toEqual(declared);

    await openChooser(fixture);
    chooserContainer(fixture)!
      .querySelector<HTMLButtonElement>('[data-testid="column-chooser-reset"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.visibleCols.map((col) => col.field)).toEqual(declared);
    expect(dataTable.isColumnWrapped(dataTable.cols[0])).toBeFalse();
    // Nothing left in storage either: a reset that leaves the old layout behind comes back on
    // the next reload.
    expect(
      localStorage.getItem(`${dataTable.resolvedStateKey}:layout`),
    ).toBeNull();
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

// The clamp hides the value and, until now, offered nothing to read it with — which is why PACMS
// hand-added [title] in four places. Wrapping is a choice someone makes; this is for the default
// nobody touched (CLAUDE.md -> decision 9). Only when it actually overflows: a title on a cell
// that fits is noise on every hover across a dense grid, which is what got the SKU tooltip
// removed in the first place.
describe('SpiderlyDataTableComponent — the clamped cell says what it hides', () => {
  it('titles a cell only while its text does not fit', async () => {
    const { fixture } = createWithDataTable(HostWithACrampedColumnComponent);
    await renderRows(fixture);
    await new Promise((resolve) => setTimeout(resolve, 30));
    fixture.detectChanges();

    const cells = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody .cell-text',
    );

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
    await new Promise((resolve) => setTimeout(resolve, 30));
    fixture.detectChanges();

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector('tbody .cell-text')!;

    expect(cell().getAttribute('title')).not.toBeNull();
    const before = dataTable.columnShare(dataTable.cols[0]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-fit"]')!
      .click();
    await renderRows(fixture);
    await new Promise((resolve) => setTimeout(resolve, 30));
    fixture.detectChanges();

    expect(dataTable.columnShare(dataTable.cols[0])).toBeGreaterThan(before);
    expect(cell().getAttribute('title')).toBeNull();
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
      { label: 'Naziv', descending: true },
    ]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-sort-asc"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.sortKeys).toEqual([
      { label: 'Naziv', descending: false },
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
// no new API (CLAUDE.md -> decision 8).
describe('SpiderlyDataTableComponent — the frozen left edge', () => {
  it('pins the identity column and the selection box, in that order', async () => {
    const { fixture } = createWithDataTable(HostWithFrozenIdentityComponent);
    await renderRows(fixture);

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
});


// The shortcut for neighbours, after the menu path rather than instead of it: HTML5 dnd has no
// edge auto-scroll, so a long move belongs in a menu, and it has no keyboard path at all
// (CLAUDE.md -> decision 6).
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
