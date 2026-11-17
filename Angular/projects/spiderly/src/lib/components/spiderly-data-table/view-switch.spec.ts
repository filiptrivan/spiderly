// Entering a view (`ViewSwitch`), through the table: its filters and its sort.

import { of } from 'rxjs';
import { renderRows } from '../../testing/spec-support.spec';
import { MatchModeCodes } from '../../enums/match-mode-enum-codes';
import { SpiderlyDataTableComponent } from './spiderly-data-table.component';
import {
  createWithDataTable,
  remount,
  viewTabs,
  clickView,
  headerSort,
  HostWithViewsComponent,
  VIEW_SORT_STATE_KEY,
  HostWithViewSortComponent,
  HostWithLateViewsComponent,
} from './spiderly-data-table.fixtures.spec';

// Wave 3. The bar lets an operator build a question; a view is the one they ask every morning,
// asked in one click. Without them the first two waves are a pile of knobs someone re-sets daily
// (decision 10).
describe('SpiderlyDataTableComponent — views', () => {
  it('applies a view when it is picked, and asks the server again', async () => {
    const { fixture, host } = createWithDataTable(HostWithViewsComponent);
    await renderRows(fixture);

    expect(viewTabs(fixture).length).toBe(2);
    expect(host.filters.applied()).toEqual([]);

    await clickView(fixture, 1);

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

    await clickView(fixture, 1);
    await clickView(fixture, 0);

    expect(host.filters.applied()).toEqual([]);
    expect(host.captured.at(-1)!.filters).toEqual({} as any);
  });
});


// Decision 10's sort half: a view whose question includes an ordering ("oldest shipped first")
// declares it, entering the view applies it, and the operator's own sort layers on top per view
// — otherwise sort stays table-global and every view switch drags the last view's ordering along.
describe('SpiderlyDataTableComponent — a view carries its sort', () => {
  const NAME_ASC = [{ field: 'name', order: 1 }];
  const STATUS_ASC = [{ field: 'status', order: 1 }];
  const ID_DESC = [{ field: 'id', order: -1 }];

  const mount = async () => {
    const mounted = createWithDataTable(HostWithViewSortComponent);
    await renderRows(mounted.fixture);
    return mounted;
  };

  it('the initial load of a restored view carries its declared sort', async () => {
    sessionStorage.setItem(
      `${VIEW_SORT_STATE_KEY}:view`,
      JSON.stringify('oldest'),
    );

    const { host } = await mount();

    expect(host.captured[0].multiSortMeta).toEqual(NAME_ASC);
  });

  // The flows-through rule `columnVisibilityOverrides` documents, applied to sort: only a
  // GESTURE writes an override, so a later change to the view's declared sort reaches every
  // operator who never touched sort.
  it('a merely-inherited sort never crystallizes into an override', async () => {
    sessionStorage.setItem(
      `${VIEW_SORT_STATE_KEY}:view`,
      JSON.stringify('oldest'),
    );

    await mount();

    expect(
      sessionStorage.getItem(`${VIEW_SORT_STATE_KEY}:oldest:sort`),
    ).toBeNull();
  });

  // The two views share one filter answer (none), so the requery effect stays silent — the
  // sort-only switch must fetch on its own or the tab lies about its ordering until page flip.
  it('entering the view applies its sort and refetches although no filter changed', async () => {
    const { fixture, host } = await mount();
    const before = host.captured.length;

    await clickView(fixture, 1);

    expect(host.captured.length).toBe(before + 1);
    expect(host.captured.at(-1)!.multiSortMeta).toEqual(NAME_ASC);
  });

  it('un-sorting inside the view lands on the view sort, not the table default', async () => {
    const { fixture, host, dataTable } = await mount();
    await clickView(fixture, 1);

    headerSort(dataTable, 'status'); // ascending — an operator override
    headerSort(dataTable, 'status'); // descending
    headerSort(dataTable, 'status'); // un-sort — substitutes the VIEW's declared sort

    expect(host.captured.at(-1)!.multiSortMeta).toEqual(NAME_ASC);
    // …and the override slot empties with it, back to declared-flows-through.
    expect(
      sessionStorage.getItem(`${VIEW_SORT_STATE_KEY}:oldest:sort`),
    ).toBeNull();
  });

  it('each view remembers the operator sort it was given', async () => {
    const { fixture, host, dataTable } = await mount();

    await clickView(fixture, 1);
    headerSort(dataTable, 'status'); // "oldest" overridden to status asc

    await clickView(fixture, 0); // "all" resets to the table default (id desc)…
    headerSort(dataTable, 'name'); // …until sorted, which becomes ITS override

    await clickView(fixture, 1);
    expect(host.captured.at(-1)!.multiSortMeta).toEqual(STATUS_ASC);

    await clickView(fixture, 0);
    expect(host.captured.at(-1)!.multiSortMeta).toEqual(NAME_ASC);
  });

  it('the operator override survives a reload', async () => {
    const first = await mount();
    await clickView(first.fixture, 1);
    headerSort(first.dataTable, 'status');

    const { host } = await remount(first.fixture, HostWithViewSortComponent);

    expect(host.captured[0].multiSortMeta).toEqual(STATUS_ASC);
  });

  // A view is a COMPLETE state, sort included (2026-09-06, grilled; supersedes the "a view
  // stating no preference keeps the ordering already there" this half of decision 10 first
  // shipped with). The carried-over ordering read as a bug in its first live week: "Poslate"'s
  // statusChangedAt rode onto "Sve", whose sorted column ships hidden — read-only chip, no
  // header to tri-state, no way out (Filip, 2026-09-06, on /orders).
  it('entering a view with no sort of its own lands on the table default', async () => {
    const { fixture, host } = await mount();
    await clickView(fixture, 1); // "oldest" applies its declared name asc

    await clickView(fixture, 0); // "all" declares nothing: the table default, never inheritance

    expect(host.captured.at(-1)!.multiSortMeta).toEqual(ID_DESC);
  });
});

// The other half of "a view is a COMPLETE state": with no override, no declared sort and no
// table default, entering a view means UNSORTED (the backend's implicit order) — deterministic —
// rather than whatever ordering the previous tab happened to leave on the grid.
describe('SpiderlyDataTableComponent — a view with no sort anywhere unsorts the grid', () => {
  it('leaving a sorted view resets, never inherits', async () => {
    const { fixture, host, dataTable } = createWithDataTable(
      HostWithLateViewsComponent,
    );
    await renderRows(fixture);

    headerSort(dataTable, 'name'); // becomes "all"'s override

    await clickView(fixture, 1);

    expect(host.captured.at(-1)!.multiSortMeta ?? []).toEqual([]);
  });

  // The restore chain used to place PrimeNG's table-global multiSortMeta between the view's
  // sort and the table default, so the last ordering ANY view used greeted every view that
  // declared nothing — on each F5, forever (the screenshotted "Sve · Sortirano po: U statusu
  // od" on /orders). On a viewed table that blob is a non-source for sort now; it still
  // carries rows/first, and stays the sort memory of view-less tables.
  it('a reload does not resurrect the table-global sort on a viewed table', async () => {
    sessionStorage.setItem(
      'sdt-late-views-spec',
      JSON.stringify({ multiSortMeta: [{ field: 'name', order: -1 }] }),
    );

    const { host } = createWithDataTable(HostWithLateViewsComponent);

    expect(host.captured[0].multiSortMeta ?? []).toEqual([]);
  });
});
