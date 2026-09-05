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
import { SpiderlyFilterTemplateDirective } from '../../directives/spiderly-filter-template.directive';
import {
  createFilterStore,
  numberFilter,
  textFilter,
} from '../../filters/filter-store';
import { SpiderlyFilterBarComponent } from '../../filters/spiderly-filter-bar.component';
import { ConfigServiceBase } from '../../services/config.service.base';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import {
  Column,
  SpiderlyDataTableComponent,
  TableView,
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

// Layout specs measure real geometry, and the Karma viewport is not evidence: local `ng test`
// runs in a real browser window sized by the display, CI in a ~800px headless one. That split
// is what let "171 green locally" coexist with a red CI twice in one day (2026-09-05: the
// frozen-offset re-split, the too-wide equality failure). Pinning every host to one width makes
// a local run measure the same share-splitting CI does, whatever monitor it runs on.
const FIXTURE_WIDTH_PX = 740;

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
  fixture.nativeElement.style.width = `${FIXTURE_WIDTH_PX}px`;
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
  it('renders the projected actions ahead of the built-in Export to Excel button', () => {
    const el: HTMLElement = createFixture(HostWithActionsComponent).nativeElement;

    const custom = el.querySelector('[data-testid="custom-action"]');
    const exportToExcel = el.querySelector('.pi-download')?.closest('button');

    expect(custom)
      .withContext('projected action button should render')
      .toBeTruthy();
    expect(exportToExcel)
      .withContext('built-in Export to Excel button should render')
      .toBeTruthy();

    // DOCUMENT_POSITION_FOLLOWING means exportToExcel appears *after* custom in DOM order.
    const relativePosition = custom!.compareDocumentPosition(exportToExcel!);
    expect(relativePosition & Node.DOCUMENT_POSITION_FOLLOWING)
      .withContext('projected action should precede Export to Excel')
      .toBeTruthy();
  });

  it('renders nothing extra when no actions template is projected', () => {
    const el: HTMLElement = createFixture(HostWithoutActionsComponent).nativeElement;

    expect(el.querySelector('[data-testid="custom-action"]')).toBeNull();
    // The built-in toolbar still renders.
    expect(el.querySelector('.pi-download')).toBeTruthy();
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
// rendering exactly as before, and the column's header and sort are untouched by it.

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
  // name and sort affordance, which is the one thing a data table must not trade for looks.
  it('leaves the templated column its header', async () => {
    const fixture = createFixture(HostWithCellTemplateComponent);
    const el = await renderRows(fixture);

    const headers = Array.from(el.querySelectorAll('thead th'));
    const idHeader = headers.find((th) => th.textContent!.includes('Id'))!;
    expect(idHeader.querySelector('p-sorticon')).toBeTruthy();
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
  // Six text columns at the 12rem default = 72rem, well past FIXTURE_WIDTH_PX — the premise
  // this suite's scroll assert rests on, held by the pin rather than the Karma window.
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
// The filter surface is the consumer's store, rendered as the chip bar. A table handed none has
// no filter surface at all — the legacy Column.filterType header filters were deleted once no
// consumer passed the old shape. See spiderly-data-table/CLAUDE.md -> "Operator-owned view".
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
  it('renders the chip bar, and nothing in the headers, when given a store', () => {
    const { fixture } = createWithDataTable(HostWithFilterStoreComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).not.toBeNull();
    expect(fixture.debugElement.queryAll(By.directive(ColumnFilter)).length).toBe(
      0,
    );
  });

  it('renders no filter surface at all when given none', () => {
    const { fixture } = createWithDataTable(HostWithoutActionsComponent);

    expect(
      fixture.debugElement.query(By.directive(SpiderlyFilterBarComponent)),
    ).toBeNull();
    expect(
      fixture.debugElement.queryAll(By.directive(ColumnFilter)).length,
    ).toBe(0);
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

// Shared by the wrap pair below on purpose: the default-cell and projected-cell wrap specs must
// measure the same grid and the same long value to stay comparable (the idAndNameCols/oneRow
// convention above).
const nameAndIdCols: Column[] = [
  { name: 'Naziv', field: 'name', filterType: 'text' },
  { name: 'Id', field: 'id', filterType: 'numeric' },
];
const longNameRow = (): Observable<PaginatedResult> =>
  paginated([{ id: 1, name: 'kupac javio da nije kod kuce do petka' }]);

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
  cols = nameAndIdCols;
  getList = longNameRow;
}

// The consumer shape the wrap contract exists for: a projected template whose markup clamps
// itself, deferring to the cell custom properties the way pa-cms's order grid does.
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

  // The toggle must reach a PROJECTED cell too, or it is a silent no-op on a fully-templated
  // table (the PACMS orders grid). How it crosses the encapsulation boundary: the td.cell-wrap
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

    const cells = (fixture.nativeElement as HTMLElement).querySelectorAll(
      'tbody .cell-text',
    );

    // Measured when the pointer arrives, which is the only moment a native title can surface —
    // and the reason there is no per-cell observer for it (see the directive).
    for (const cell of Array.from(cells)) {
      cell.dispatchEvent(new PointerEvent('pointerenter', { bubbles: false }));
    }
    fixture.detectChanges();

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

    const cell = () =>
      (fixture.nativeElement as HTMLElement).querySelector('tbody .cell-text')!;
    const hover = () =>
      cell().dispatchEvent(new PointerEvent('pointerenter', { bubbles: false }));

    hover();
    expect(cell().getAttribute('title')).not.toBeNull();
    const before = dataTable.columnShare(dataTable.cols[0]);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-fit"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.columnShare(dataTable.cols[0])).toBeGreaterThan(before);
    hover();
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
  // The measured offset's correction lands at animation-frame timing `whenStable` never awaits
  // and needs a `detectChanges` to reach the DOM — CLAUDE.md's ResizeObserver trap bullet
  // carries the full telling. Settle until the header invariant holds; the body cells bind the
  // same `frozenOffsetPx`, so they converge with it, and on non-convergence the asserts below
  // fail loudly with the real values.
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


@Component({
  imports: [SpiderlyDataTableComponent, SpiderlyFilterTemplateDirective],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [getPaginatedListObservableMethod]="getList"
    >
      <ng-template spiderlyFilterTemplate="name" let-f>
        <input
          data-testid="custom-filter"
          [value]="f.value() ?? \'\'"
          (input)="f.set({ operator: matchMode, value: $any($event.target).value })"
        />
      </ng-template>
    </spiderly-data-table>
  `,
})
class HostWithFilterTemplateComponent {
  matchMode = MatchModeCodes.Contains;
  filters = createFilterStore({
    name: textFilter({ label: 'Naziv' }),
    id: numberFilter({ label: 'Id' }),
  });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text', filterId: 'name' },
    { name: 'Id', field: 'id', filterType: 'numeric', filterId: 'id' },
  ];
  getList = emptyList;
}

// The narrow job the directive has. Placing a filter ANYWHERE else — a drawer, a modal, a header
// cell — needs no directive: store.get(id) hands back the same handle and depends on nothing in
// the component tree, which is the whole reason the store belongs to the consumer.
describe('SpiderlyDataTableComponent — a projected filter control', () => {
  it('renders the consumer template for that filter, and the default for the rest', async () => {
    const { fixture } = createWithDataTable(HostWithFilterTemplateComponent);
    await renderRows(fixture);
    const el = fixture.nativeElement as HTMLElement;

    await openColumnMenu(fixture, 0);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-filter"]')!
      .click();
    await renderRows(fixture);

    const custom = el.querySelector<HTMLInputElement>(
      '[data-testid="custom-filter"]',
    );
    expect(custom).not.toBeNull();
    expect(el.querySelector('[data-testid="filter-editor-value"]')).toBeNull();

    // It drives the same store the built-in control does.
    custom!.value = 'Bosch';
    custom!.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    (fixture.nativeElement as HTMLElement)
      .querySelector<HTMLButtonElement>('[data-testid="filter-editor-apply"]')!
      .click();
    await renderRows(fixture);

    expect(fixture.componentInstance.filters.applied()[0].value).toBe('Bosch');

    // The filter that projects nothing keeps the control the bar would have drawn.
    await openColumnMenu(fixture, 1);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-filter"]')!
      .click();
    await renderRows(fixture);

    expect(el.querySelector('[data-testid="filter-editor-value"]')).not.toBeNull();
  });
});


@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithViewsComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Status', field: 'status', filterType: 'numeric' },
  ];
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    {
      id: 'packing',
      label: 'Za pakovanje',
      apply: (filters) => {
        filters.set('status', { operator: MatchModeCodes.Equals, value: 2 });
        filters.commit('status');
      },
    },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

// Wave 3. The bar lets an operator build a question; a view is the one they ask every morning,
// asked in one click. Without them the first two waves are a pile of knobs someone re-sets daily
// (CLAUDE.md -> decision 10).
describe('SpiderlyDataTableComponent — views', () => {
  function selectView(fixture: ComponentFixture<unknown>, index: number): void {
    (fixture.nativeElement as HTMLElement)
      .querySelectorAll<HTMLButtonElement>('[data-testid="table-view"]')
      [index].click();
    fixture.detectChanges();
  }

  it('applies a view when it is picked, and asks the server again', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    expect(
      (fixture.nativeElement as HTMLElement).querySelectorAll(
        '[data-testid="table-view"]',
      ).length,
    ).toBe(2);
    expect(host.filters.applied()).toEqual([]);

    await fixture.ngZone!.run(async () => selectView(fixture, 1));
    await renderRows(fixture);

    expect(host.filters.applied().map((chip) => chip.id)).toEqual(['status']);
    expect(host.captured.at(-1)!.filters).toEqual({
      status: [{ matchMode: MatchModeCodes.Equals, value: 2 }],
    } as any);
  });

  // A view is a STATE, not an addition: going back to "all" has to leave nothing of the last one
  // behind, or the two compose into a question nobody asked.
  it('replaces the previous view rather than adding to it', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    await fixture.ngZone!.run(async () => selectView(fixture, 1));
    await renderRows(fixture);
    await fixture.ngZone!.run(async () => selectView(fixture, 0));
    await renderRows(fixture);

    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });
});


@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-transient-view-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithTransientViewComponent {
  filters = createFilterStore({ createdAt: numberFilter({ label: 'Datum' }) });
  cols: Column[] = [{ name: 'Naziv', field: 'name', filterType: 'text' }];
  applyCount = 0;
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    {
      id: 'today',
      label: 'Danas primljene',
      transient: true,
      apply: (filters) => {
        filters.set('createdAt', {
          operator: MatchModeCodes.Equals,
          value: ++this.applyCount,
        });
        filters.commit('createdAt');
      },
    },
  ];
  getList = emptyList;
}

// A view whose apply is a function of NOW cannot live under stored-wins-over-apply: "Danas
// primljene" clicked yesterday stores yesterday's midnight, and today's click would restore it —
// a tab named "today" showing yesterday, with only the chip's date to give it away. A transient
// view re-derives instead of remembering.
describe('SpiderlyDataTableComponent — transient views', () => {
  it('re-applies on every select instead of restoring what an earlier visit stored', async () => {
    const { fixture, host } = createWithDataTable(HostWithTransientViewComponent);
    await renderRows(fixture);

    const views = () =>
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '[data-testid="table-view"]',
      );

    await fixture.ngZone!.run(async () => views()[1].click());
    await renderRows(fixture);
    expect(host.filters.applied()[0].value).toBe(1);

    await fixture.ngZone!.run(async () => views()[0].click());
    await renderRows(fixture);

    await fixture.ngZone!.run(async () => views()[1].click());
    await renderRows(fixture);

    expect(host.filters.applied()[0].value)
      .withContext('the second visit must re-derive, not remember')
      .toBe(2);
  });
});

// The other half of decision 10, and the reason a view is more than a saved filter: a picking view
// and a payments view want different COLUMNS, not just different rows. Layout global to the table
// would re-create the original complaint one level up.
describe('SpiderlyDataTableComponent — layout is scoped to the view', () => {
  it('keeps each view\'s wrapped columns apart', async () => {
    const { fixture, dataTable } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    const views = () =>
      (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
        '[data-testid="table-view"]',
      );

    await fixture.ngZone!.run(async () => views()[0].click());
    await renderRows(fixture);

    await openColumnMenu(fixture);
    columnMenu(fixture)
      .querySelector<HTMLButtonElement>('[data-testid="column-menu-wrap"]')!
      .click();
    await renderRows(fixture);

    expect(dataTable.isColumnWrapped(dataTable.cols[0])).toBeTrue();

    await fixture.ngZone!.run(async () => views()[1].click());
    await renderRows(fixture);

    expect(dataTable.isColumnWrapped(dataTable.cols[0]))
      .withContext('the other view must not inherit it')
      .toBeFalse();

    await fixture.ngZone!.run(async () => views()[0].click());
    await renderRows(fixture);

    expect(dataTable.isColumnWrapped(dataTable.cols[0]))
      .withContext('and going back must find it again')
      .toBeTrue();
  });
});


@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="'sdt-store-hide-keeps-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithSortableFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text' },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

// Decision 2b's other half: the whole "hidden contributes nothing" apparatus exists because the
// HEADER was the only place a constraint could be seen. On a store table the chip bar names the
// filters and the sort chip names the sort, so hiding a column no longer silently hides what the
// grid is narrowed or ordered by — and dropping the constraint on hide becomes the destructive
// act, not the safe one.
describe('SpiderlyDataTableComponent — a store table hides columns without touching constraints', () => {
  it('hiding a sorted column keeps the sort: the sort chip names it now', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithSortableFilterStoreComponent,
    );
    await renderRows(fixture);

    await fixture.ngZone!.run(async () =>
      dataTable.table.sort({
        originalEvent: new MouseEvent('click'),
        field: 'name',
      }),
    );
    await renderRows(fixture);
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Naziv');
    await renderRows(fixture);

    expect(host.captured.length)
      .withContext('a hide is a layout change, not a query change')
      .toBe(loadsBefore);

    await fixture.ngZone!.run(async () => dataTable.reload());
    await renderRows(fixture);

    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some(
        (meta) => meta.field === 'name',
      ),
    )
      .withContext('the hidden column must keep sorting')
      .toBeTrue();
  });
});

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="'sdt-store-no-phantom-reveal-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithHiddenColumnFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text', visible: false },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  getList = emptyList;
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [defaultSortField]="'name'"
      [stateKey]="'sdt-store-hidden-default-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithHiddenDefaultSortFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text', visible: false },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — a store table default-sorts by a hidden column', () => {
  // The legacy null-on-hidden exists so clear-on-hide is not undone by the default sneaking back
  // in — an invisible ordering. On a store table the sort chip names the default like any other
  // sort, so a hidden column is as good a default as a visible one.
  it('applies the declared default sort even while its column is hidden', () => {
    const fixture = createFixture(HostWithHiddenDefaultSortFilterStoreComponent);
    const host = fixture.componentInstance;

    expect(host.captured[0].multiSortMeta).toEqual([
      { field: 'name', order: 1 },
    ]);
  });
});

// A table migrated to the bar reads the same PrimeNG state key its header-filter days wrote, and
// that blob can still hold constraint meta. Under the store those constraints never reach a
// request (the payload replaces them), so revealing a hidden column for them resurrects a phantom
// — and on a localStorage table it does so on EVERY load, because nothing ever rewrites the stale
// blob. PACMS's order list is exactly this: stateStorage 'local', operators with weeks of
// header-filter state.
describe('SpiderlyDataTableComponent — a store table ignores stale header-filter state', () => {
  it('does not reveal a hidden column for persisted header-filter meta', () => {
    sessionStorage.setItem(
      'sdt-store-no-phantom-reveal-spec',
      JSON.stringify({
        filters: { name: [{ value: 'abc', matchMode: 'contains' }] },
      }),
    );

    const fixture = createFixture(HostWithHiddenColumnFilterStoreComponent);

    expect(
      headerTexts(fixture.nativeElement).some((header) =>
        header.includes('Naziv'),
      ),
    )
      .withContext('a constraint the store never sends must not reveal anything')
      .toBeFalse();
  });
});

// The header filters were persisted for free by PrimeNG's stateful table; the bar owns them now,
// so nothing was writing them and a refresh dropped every one (Filip, on /tags). This is the
// regression that shipped with the bar, not a new capability.
describe('SpiderlyDataTableComponent — applied filters survive a reload', () => {
  it('carries them into the first request of the next mount', async () => {
    const first = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(first.fixture);

    await first.fixture.ngZone!.run(async () => {
      first.host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      first.host.filters.commit('companyName');
    });
    await renderRows(first.fixture);
    first.fixture.destroy();
    // A reload, as far as this component is concerned: a brand-new module and a brand-new store,
    // with only the storage carried across.
    TestBed.resetTestingModule();

    const next = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(next.fixture);

    expect(next.host.filters.applied().map((chip) => chip.id)).toEqual([
      'companyName',
    ]);
    expect(next.host.captured[0].filters).toEqual({
      companyName: [
        { matchMode: MatchModeCodes.Contains, value: 'Elektromont' },
      ],
    } as any);
  });

  it('forgets them when the bar is cleared', async () => {
    const first = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(first.fixture);

    await first.fixture.ngZone!.run(async () => {
      first.host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      first.host.filters.commit('companyName');
      first.host.filters.clear();
    });
    await renderRows(first.fixture);
    first.fixture.destroy();
    // A reload, as far as this component is concerned: a brand-new module and a brand-new store,
    // with only the storage carried across.
    TestBed.resetTestingModule();

    const next = createWithDataTable(HostWithCapturingFilterStoreComponent);
    await renderRows(next.fixture);

    expect(next.host.filters.applied()).toEqual([]);
  });
});

// ---------------------------------------------------------------------------
// STORELESS tables (the generated M2M details grids, until spiderly#407 scaffolds their stores)
// have no bar — so nothing on screen can name a hidden column's sort. For them alone, the old
// "hidden contributes nothing" rule survives on the SORT axis: hiding a sorted column drops its
// sort, and a hidden defaultSortField column does not apply. Store tables keep decision 2b.
// ---------------------------------------------------------------------------

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [stateKey]="'sdt-storeless-hidden-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostStorelessSortableComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [defaultSortField]="'id'"
      [stateKey]="'sdt-storeless-hidden-default-sort-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostStorelessDefaultSortComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

describe('SpiderlyDataTableComponent — a storeless table drops a hidden column\'s sort', () => {
  it('hiding a sorted column removes its sort and reloads exactly once, without it', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostStorelessSortableComponent,
    );

    dataTable.table.sort({
      originalEvent: new MouseEvent('click'),
      field: 'name',
    });
    fixture.detectChanges();
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(host.captured.length)
      .withContext('one reload after hiding a sorted column')
      .toBe(loadsBefore + 1);
    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some((m) => m.field === 'name'),
    )
      .withContext('a hidden column must not sort a table with no surface to name it')
      .toBeFalse();
  });

  it('does not reload when hiding an unsorted column', async () => {
    const { fixture, host } = createWithDataTable(HostStorelessSortableComponent);
    const loadsBefore = host.captured.length;

    await openChooser(fixture);
    clickOption(fixture, 'Name');

    expect(host.captured.length).toBe(loadsBefore);
  });

  it('leaves the reload unsorted instead of re-applying an invisible default sort', async () => {
    const { fixture, host } = createWithDataTable(HostStorelessDefaultSortComponent);
    expect(host.captured[0].multiSortMeta).toEqual([{ field: 'id', order: 1 }]);

    await openChooser(fixture);
    clickOption(fixture, 'Id');

    expect(
      (host.captured.at(-1)!.multiSortMeta ?? []).some((m) => m.field === 'id'),
    )
      .withContext('hidden default-sort column must not sort a storeless table')
      .toBeFalse();
  });
});

const REQUERY_PERSIST_STATE_KEY = 'sdt-requery-persist-spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [stateKey]="stateKey"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
class HostWithPersistedFilterStoreComponent {
  cols = cols;
  stateKey = REQUERY_PERSIST_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — a committed filter re-persists page one', () => {
  // The commit path resets table.first to 0 but PrimeNG saves state only from its own
  // interactions — so without an explicit re-persist, the pre-commit page offset survives in
  // storage and the NEXT visit replays it against the narrowed result set: the backend answers
  // data=[] for an offset the filtered total no longer reaches, and the grid renders empty
  // under chips claiming matches.
  it('overwrites a persisted deep page offset with 0 at commit', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithPersistedFilterStoreComponent,
    );
    await renderRows(fixture);

    fixture.ngZone!.run(() => {
      dataTable.table.first = 100;
      dataTable.table.saveState();
    });

    await fixture.ngZone!.run(async () => {
      host.filters.set('companyName', {
        operator: MatchModeCodes.Contains,
        value: 'Elektromont',
      });
      host.filters.commit('companyName');
    });
    await renderRows(fixture);

    const persisted = JSON.parse(
      sessionStorage.getItem(REQUERY_PERSIST_STATE_KEY) ?? '{}',
    );
    expect(persisted.first)
      .withContext('the persisted offset must follow the commit back to page one')
      .toBe(0);
  });
});

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [hasLazyLoad]="false"
      [getFormArrayItems]="getItems"
    ></spiderly-data-table>
  `,
})
class HostClientSideWithStoreComponent {
  cols = cols;
  getItems = () => [{ id: 1 }];
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

describe('SpiderlyDataTableComponent — a store on a client-side table fails loud', () => {
  // Armed anyway, the first commit would call the absent getPaginatedListObservableMethod
  // inside the requery effect and strand the pending overlay forever — a half-broken table
  // that looks fine until the first Apply. Client-side store predicates are unbuilt.
  it('throws at init instead of stranding the first commit', () => {
    expect(() => createFixture(HostClientSideWithStoreComponent)).toThrowError(
      /\[filters\] requires a lazy table/,
    );
  });
});
