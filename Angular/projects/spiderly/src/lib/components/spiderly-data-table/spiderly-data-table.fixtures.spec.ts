// The data table specs' shared harness: the mount, the storage reset, and the hosts and helpers
// that two spec files share. Named *.spec.ts, as spec-support.spec.ts is, so tsconfig.lib leaves
// it out of the library build. It holds no tests.

import { Component, ErrorHandler } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideNoopAnimations } from '@angular/platform-browser/animations';
import { provideRouter } from '@angular/router';
import { TranslocoTestingModule } from '@jsverse/transloco';
import { DialogService } from 'primeng/dynamicdialog';
import { Popover } from 'primeng/popover';
import { Observable, of } from 'rxjs';
import { delay } from 'rxjs/operators';
import { paginated, renderRows, translocoTesting } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { Filter } from '../../entities/filter';
import { PaginatedResult } from '../../entities/paginated-result';
import { createFilterStore, numberFilter, textFilter } from '../../filters/filter-store';
import { ConfigServiceBase } from '../../services/config.service.base';
import { SpiderlyMessageService } from '../../services/spiderly-message.service';
import { Column, SpiderlyDataTableComponent, TableView } from './spiderly-data-table.component';

export const cols: Column[] = [{ name: 'Id', field: 'id', filterType: 'numeric' }];

export const emptyList = (): Observable<PaginatedResult> => paginated([]);

// Snapshot each filter — PrimeNG mutates/reuses the lazy-load event object.
export const capturingGetList =
  (captured: Filter[]) =>
  (filter: Filter): Observable<PaginatedResult> => {
    captured.push(JSON.parse(JSON.stringify(filter)));
    return emptyList();
  };

// The library's error owner, stubbed so a spec can assert a failure reached it. Reset with the
// stores below, since Jasmine spies persist across the specs in a file.
export const errorHandler = { handleError: jasmine.createSpy('handleError') };

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
export const FIXTURE_WIDTH_PX = 740;

export function createFixture<T>(host: new () => T): ComponentFixture<T> {
  TestBed.configureTestingModule({
    imports: [
      host,
      TranslocoTestingModule.forRoot(
        translocoTesting({ ResultCount: '{{count}} results', Yes: 'Yes', No: 'No' }),
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

export function createWithDataTable<T>(host: new () => T): {
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

// A reload, as far as this component is concerned: a brand-new module and a brand-new store,
// with only the storage carried across.
export async function remount<T>(
  fixture: ComponentFixture<unknown>,
  host: new () => T,
): Promise<{
  fixture: ComponentFixture<T>;
  host: T;
  dataTable: SpiderlyDataTableComponent;
}> {
  fixture.destroy();
  TestBed.resetTestingModule();
  const next = createWithDataTable(host);
  await renderRows(next.fixture);
  return next;
}

export const viewTabs = (fixture: ComponentFixture<unknown>) =>
  (fixture.nativeElement as HTMLElement).querySelectorAll<HTMLButtonElement>(
    '[data-testid="table-view"]',
  );

export async function clickView(
  fixture: ComponentFixture<unknown>,
  index: number,
): Promise<void> {
  await fixture.ngZone!.run(async () => viewTabs(fixture)[index].click());
  await renderRows(fixture);
}

// The tri-state header gesture, as PrimeNG dispatches it.
export const headerSort = (dataTable: SpiderlyDataTableComponent, field: string) =>
  dataTable.table.sort({ originalEvent: new MouseEvent('click'), field });

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
export class HostWithoutActionsComponent {
  cols = cols;
  getList = emptyList;
}

export const DEFAULT_SORT_STATE_KEY = 'sdt-default-sort-spec';

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
export class HostWithDefaultSortComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  stateKey = DEFAULT_SORT_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

export function headerCells(el: HTMLElement): HTMLTableCellElement[] {
  return Array.from(el.querySelectorAll('th'));
}

// Async because the checkboxes' [ngModel] writes resolve in a microtask. A spec awaits
// `fixture.whenStable()` after it opens the chooser or toggles a box. Once open, PrimeNG appends
// the popover to `document.body`. So a spec reads the `Popover` instance's `container` (see
// overlayHolding), not the document: stale popovers from earlier fixtures stay there.
export async function openChooser(fixture: ComponentFixture<unknown>): Promise<void> {
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

export function columnMenu(fixture: ComponentFixture<unknown>): HTMLElement {
  return overlayHolding(fixture, '[data-testid="column-menu-hide"]')!;
}

// Query THIS fixture's popover container — once open, PrimeNG appends it to
// document.body, where stale popovers from earlier fixtures may also linger.
export function chooserContainer(
  fixture: ComponentFixture<unknown>,
): HTMLElement | undefined {
  return overlayHolding(fixture, '.column-chooser');
}

export function chooserOptions(
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

export function clickOption(fixture: ComponentFixture<unknown>, label: string): void {
  chooserOptions(fixture)
    .find((o) => o.label === label)!
    .input.click();
  fixture.detectChanges();
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
export class HostWithLockedColumnComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric', lockVisible: true },
    { name: 'Name', field: 'name', filterType: 'text' },
  ];
  getList = emptyList;
}

export const idAndNameCols: Column[] = [
  { name: 'Id', field: 'id', filterType: 'numeric' },
  { name: 'Name', field: 'name', filterType: 'text' },
];

// id is fractional and four digits so the FORMATTED value ("1,234.5" under the test locale) is
// distinguishable from the raw one (1234.5) — that is what tells the two context members apart.
export const oneRow = (): Observable<PaginatedResult> =>
  paginated([{ id: 1234.5, name: 'Ana' }]);

// Two values whose only difference is how much room the text WANTS. Under the browser's default
// `table-layout: auto` that difference is what sized the columns.
const SHORT_NAME = 'Ana';
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
export class HostWithSwappableRowsComponent {
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
export async function swapRowNameTo(
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

export const fourSelectableRows = (): Observable<PaginatedResult> =>
  paginated([{ id: 1 }, { id: 2 }, { id: 3 }, { id: 4 }]);

/** createWithDataTable + the settle-and-render ritual (renderRows), for suites that need rows. */
export async function renderStable<T>(host: new () => T): Promise<{
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
export class HostWithSelectionComponent {
  cols = cols;
  getList = fourSelectableRows;
}

/** Presses a row's selection checkbox the way a user does: mousedown on the cell, click on the input. */
export function clickRowCheckbox(
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
export class HostWithCapturingFilterStoreComponent {
  cols = cols;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
  filters = createFilterStore({
    companyName: textFilter({ label: 'Firma' }),
  });
}

// Shared by the wrap pair below on purpose: the default-cell and projected-cell wrap specs must
// measure the same grid and the same long value to stay comparable (the idAndNameCols/oneRow
// convention above).
export const nameAndIdCols: Column[] = [
  { name: 'Naziv', field: 'name', filterType: 'text' },
  { name: 'Id', field: 'id', filterType: 'numeric' },
];
export const longNameRow = (): Observable<PaginatedResult> =>
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
export class HostWithTwoColumnsComponent {
  cols = nameAndIdCols;
  getList = longNameRow;
}

export async function openColumnMenu(
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

// One home for the view both view-hosts declare — two describes assert its filter shape, so a
// second copy would let them drift apart. Arrival timing is what differs between the hosts.
export const packingView: TableView = {
  id: 'packing',
  label: 'Za pakovanje',
  apply: (filters) => {
    filters.set('status', { operator: MatchModeCodes.Equals, value: 2 });
    filters.commit('status');
  },
};

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
export class HostWithViewsComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Status', field: 'status', filterType: 'numeric' },
  ];
  views: TableView[] = [{ id: 'all', label: 'Sve' }, packingView];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

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
export class HostWithTransientViewComponent {
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

export const VIEW_SORT_STATE_KEY = 'sdt-view-sort-spec';

@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="stateKey"
      [defaultSortField]="'id'"
      [defaultSortOrder]="-1"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
export class HostWithViewSortComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Status', field: 'status', filterType: 'numeric' },
  ];
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    { id: 'oldest', label: 'Najstarije', sort: [{ field: 'name', order: 1 }] },
  ];
  stateKey = VIEW_SORT_STATE_KEY;
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}

// The order-list shape: a view licensed by an async catalog ("Za pakovanje" exists only once a
// status row claims awaitsCourierPickup) is NOT in [views] at the table's ngOnInit — it arrives
// on a later input change. A restore that only looks at init would miss it on every reload.
@Component({
  imports: [SpiderlyDataTableComponent],
  template: `
    <spiderly-data-table
      [cols]="cols"
      [filters]="filters"
      [views]="views"
      [stateKey]="'sdt-late-views-spec'"
      [getPaginatedListObservableMethod]="getList"
    ></spiderly-data-table>
  `,
})
export class HostWithLateViewsComponent {
  filters = createFilterStore({ status: numberFilter({ label: 'Status' }) });
  cols: Column[] = [
    { name: 'Naziv', field: 'name', filterType: 'text' },
    { name: 'Status', field: 'status', filterType: 'numeric' },
  ];
  views: TableView[] = [
    { id: 'all', label: 'Sve' },
    { id: 'companies', label: 'Pravna lica' },
  ];
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);

  revealPacking(): void {
    this.views = [...this.views, packingView];
  }
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
export class HostWithHiddenDefaultSortFilterStoreComponent {
  cols: Column[] = [
    { name: 'Id', field: 'id', filterType: 'numeric' },
    { name: 'Naziv', field: 'name', filterType: 'text', visible: false },
  ];
  filters = createFilterStore({ name: textFilter({ label: 'Naziv' }) });
  captured: Filter[] = [];
  getList = capturingGetList(this.captured);
}
