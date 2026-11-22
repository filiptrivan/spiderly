// Loading the rows (`PageLoader`), through the table: the page sizes, the scroll on a page change,
// and the pending state.

import { Component, computed, ErrorHandler } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';
import {
  expectPendingVeil,
  paginated,
  renderRows,
  tbodyText,
} from '../../testing/spec-support.spec';
import { PaginatedResult } from '../../entities/paginated-result';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  cols,
  emptyList,
  errorHandler,
  createFixture,
  createWithDataTable,
  HostWithoutActionsComponent,
  HostWithDefaultSortComponent,
  swapRowNameTo,
  renderStable,
  HostWithSelectionComponent,
} from './spiderly-data-table.fixtures.spec';

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

  // Pins the seam described on the scroll-target-below-top-chrome mixin (styles/layout/_mixins.scss):
  // the component reads the inset from a var it does not own, and still works when no shell declares one.
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
  // The ::ng-deep veil rules target PrimeNG's mask, which this component does not author. A PrimeNG
  // change can stop them silently, and this computed-style assert notices. The pin table is shared
  // with the data view's suite, because the declaration is: table-pending-veil() in
  // styles/layout/_mixins.scss.
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
