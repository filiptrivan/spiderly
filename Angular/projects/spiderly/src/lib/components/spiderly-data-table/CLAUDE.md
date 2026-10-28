# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

## Where the rest lives

| Key | When, and where |
|---|---|
| Operator-owned view | Re-deciding, extending or undoing the 2026-09-02 rework (the consumer-owned filter store and its chip bar, the `▾` header menu, widths as minimums, header drag, the frozen left edge, truncation and its overflow title, views with their filters, sort and declared columns), or reviving what it rejected, parked (custom views) or deleted (the legacy header-filter path): its decision record, `docs/data-table-operator-owned-view.md`. The decisions this directory's comments cite ("decision 8", "CLAUDE.md -> decision 8") are numbered there. |

## Traps

**Two traps found by a spec, neither visible by eye:**

- **`ResizeObserver`'s first callback lands an animation frame after paint.** The frozen column
  sat at `left: 0`, on top of the checkbox column it starts after, for that frame — and its own
  spec had been passing on timing luck until adding suites reordered the run. Measure once on a
  microtask as well; writing it inline in `ngAfterViewInit` is an NG0100. The microtask measure
  itself races the first row render (rows land on a macrotask), so where the rendered rows
  re-split the shares the correction arrives at frame timing `whenStable` never waits for —
  Linux headless Chrome re-split by 2px while macOS did not (CI run 33943130146, 2026-09-05),
  a green local suite proving nothing. A spec asserting the offset must settle animation frames
  first: `settleFrozenOffset` in the spec.
- **PrimeNG's `p-inputtext-sm` does nothing on a `p-select`.** Every component has its own small
  class, so size controls through the component's `size` input, never a borrowed class.

**A trap that cost an afternoon:**

- **PrimeNG does not re-project content an `@if` inside a popover has destroyed.** Reopening gives
  a container with `overlayVisible` true and nothing in it. Render the menu items once and hold
  the target in a field (`menuColumn`); never key the popover's content on the thing it acts on.

## Custom toolbar actions — `<ng-template spiderlyDataTableActions>`

The slot's contract is the doc comment on `SpiderlyDataTableActionsDirective`. Editing notes:

- **If you reorder the caption, keep the outlet first**: the directive's doc says why the slot leads the built-ins.
- **No context is passed** to the template, deliberately: lazy-load selection has no clean flat-id representation (`newlySelectedItems`/`unselectedItems` under select-all).

## Shift-click range selection — anchor model

Selection checkboxes support Gmail-style ranges: shift+click applies the clicked checkbox's new state to every row between it and the anchor (`rangeAnchorId`). Consumer-facing behavior: `claude-plugins/docs/angular-customization/index.md` → "Row selection". Editing notes:

- **The shift state lives on the mousedown, not on the checkbox event.** `p-checkbox` emits the DOM `change` event as `onChange.originalEvent`, and `change` carries no `shiftKey` — so `onSelectionCellMouseDown` captures it into `pendingShift`, which the same click's `selectRow` consumes-and-clears. Don't "simplify" back to reading the event.
  - **Clearing the flag on the cell's `click` does NOT work** and was tried: the checkbox's `change` fires *after* click dispatch completes, so a click-time clear runs first and kills every real range (it reds the whole range suite — that failure is the pin). The residual, accepted: press the checkbox, drag off, release outside, then toggle that same row by keyboard — it ranges. Closing it needs a document-level `mouseup`, which has the same ordering problem.
- **Ranges resolve over `renderedRows()` — the display window — never over raw `items` or `rowData.index`.** It delegates to PrimeNG's own `table.dataToRender(null)` (`filteredValue || value`, sliced by the paginator, lazy capped at `rows`) precisely so it cannot drift from what the table paints; hand-rolling the slice re-introduced a gap where a server overshooting `Rows` would let a range sweep unrendered rows. An `items`-based range would sweep filtered-out or off-page rows sitting between two visually adjacent ones (`rowData.index` can't address a range at all — only `loadFormArrayItems` stamps it). The invariant is validate-at-use: an anchor outside the window misses `findIndex` and the shift-click degrades to a plain toggle, so page flips and client-side sort/filter need no per-path resets. The only explicit resets are where rows are wholesale replaced — `lazyLoad` (the decided reset-on-reload semantics for lazy tables) and `loadFormArrayItems`.
- **Every change goes through `toggleRow`** (the former `selectRow` body): that is what keeps the lazy delta model (`newlySelectedItems`/`unselectedItems`) and per-row event emissions identical to manual clicks. Rows already in the target state are skipped — the skip is what prevents duplicate ids in the delta arrays.

## Row navigation vs interactive cells — one seam, `.row-interactive`

The seam, its carriers and the rule for a new interactive cell: the doc comment on `onRowClick`. Editing notes:

- **The marker sits on the action `<span>`s, not their flex container** — that container renders in *every* non-actions cell too (empty), so marking it would have made a strip of every ordinary cell non-navigating.
- `onCellClick` keeps its own `stopPropagation` (documented consumer contract, and it also shields consumers' outer handlers); it is belt now, not the mechanism.

## Operator labels are runtime translations

The bar's operator picker and chip phrases are Transloco keys (`OPERATOR_WORDS`, `filters/filter-store.ts`), so the user-visible text is the **value** in `assets/i18n/<locale>.json`, not the key. When matching options programmatically (e2e tests, conditional logic), match against the rendered label, not the key; renaming a key without updating en.json (or vice versa) silently breaks consumers that match by label.

## Column chooser — `Column.visible` / `Column.lockVisible`

Consumer-facing behavior is documented in `claude-plugins/docs/angular-customization/index.md` → "Column chooser". Editing notes:

- Template loops iterate `visibleCols` (recomputed via `refreshVisibleCols()`), never `cols`.
- The chooser only renders for lazy tables (`hasLazyLoad`); client-side form-array tables keep their declared columns.
- Spec gotcha: the chooser checkboxes' `[ngModel]` writes resolve in a microtask — tests must `await fixture.whenStable()` after opening/toggling (see `openChooser` in the spec). Once open, PrimeNG appends the popover to `document.body`, so specs query the `Popover` instance's `container`, never the document (stale popovers from earlier fixtures linger there).

## Rows-per-page — `rowsPerPageOptions`

The merge rules: the doc comment on `mergeActivePageSizesIntoOptions`. The merge must run after `resolvedStateKey` is derived, or the persisted read is a silent null. The user's pick persists for free through PrimeNG table state (`saveState`/`restoreState` carry `rows`) — do NOT add custom persistence; a durable (localStorage) page size was considered and deliberately deferred (2026-08-17) until usage asks for it.

## Scroll back to the table on page change — `onPageChange`

Paging leaves the reader parked at the paginator with the new rows off-screen above it, so `onPageChange` puts the wrapper `<div>` back under the top of the viewport. Unconditional, with no opt-out `@Input`: PrimeNG holds the same policy for `[scrollable]` tables (`Table.onPageChange` ends in `resetScrollTop()`) and offers no flag either. That branch never runs for us because we drive `responsiveLayout="scroll"`, not `scrollable` — the window is what scrolls here, not a container inside the table.

- **The hook is `(onPage)`, not `lazyLoad`.** `Paginator.onRppChange` routes the page-size dropdown through `changePage()` → `Table.onPageChange` → `onPage`, so one binding covers page navigation *and* page size, on lazy and client-side tables alike. `lazyLoad` also fires for sort and filter, where the user is already up at the header. **`restoreState` never emits `onPage`** — it assigns `first`/`rows` directly and emits only `firstChange` — which is why a restored page offset does not scroll on first paint. That is spec-pinned, because a scroll there would shove a details page's form off-screen the moment it opens.
- **The target is the `#tableContainer` wrapper, not the host.** `:host` declares no `display`, so `<spiderly-data-table>` is `display: inline` and its `getBoundingClientRect()` is not a dependable box for the "is it above the viewport" test. The template hands the element straight to the handler — `(onPage)="onPageChange(tableContainer)"` — so there is no `@ViewChild` and no nullable plumbing field on the component's public surface.
- **The guard is an explicit `rect.top < scrollMarginTop`, not `block: 'nearest'`.** `'nearest'` looks like it would skip a needless scroll for free, but its behaviour when the element overruns the viewport at both edges is the case we could not confirm against CSSOM-View (both the W3C and csswg copies truncate before the algorithm, checked 2026-08-29). The explicit check is two lines and testable.
- **The offset lives in CSS and is read back from CSS**: its one declaration is the `scroll-target-below-top-chrome` mixin (`styles/layout/_mixins.scss`, its comment). Why `behavior: 'instant'` and why the offset is read rather than passed: the comment block on `scrollElementIntoViewIfAboveViewport`, which is the single telling.
- **`spiderly-data-view` shares the helper and the mixin**, having had the identical wrapper and the identical bug. Its scroll binding is still not covered — every spec here mounts `SpiderlyDataTableComponent` — but the component does have a spec file now (`spiderly-data-view.component.spec.ts`, added 2026-08-30 with the pending-state work), so adding a scroll case there is cheap when someone wants it.

## Pending state — one predicate, stale rows, and a veil

Why every refetch raises `loading` and leaves `items` alone: the comment opening `lazyLoad`. Before 2026-08-30 `lazyLoad` never raised the flag at all — only `reload()` did — so paging, sorting and filtering ran with no feedback whatsoever, which is invisible on a fast query and glaring on a slow one.

- **`isPending`, the single predicate (its doc comment), is not a11y parity**: `aria-busy` defers announcements, it does not produce one, and a screen-reader user is still told nothing when a page flip starts or lands. There is no live region to announce into — zero `aria-live` in this library and none in PrimeNG 19.1.3's table or paginator (checked 2026-08-30). Adding one is a library-wide decision needing an `sr-only` utility and a seeded Transloco key, not a rider on this. There is deliberately no opt-out `@Input`.
- **There is no `loadingbody` template, on purpose** (why PrimeNG's would wedge a row under the page: the spec's "exactly the data rows" assert). It was deleted rather than `*ngIf`-guarded — with the overlay up it added nothing, the data view never had one, and it carried an `[attr.colspan]` that had to track `visibleCols` plus the selection column forever.
- **The overlay's styling is the `table-pending-veil` mixin** (`styles/layout/_mixins.scss`), shared with `spiderly-data-view` and pinned once by `expectPendingVeil` in `testing/spec-support.spec.ts`. Why PrimeNG's own scrim had to go, why `color` is set alongside the background, and why the spinner is top-anchored: the mixin's doc comment, which is the single telling.
- **`loading = false` stays after the selection await, deliberately** (the eager selected-ids request and the `try/finally` around that await: the comments in `lazyLoad`): lowering it earlier paints page 2's row 5 as checked because page 1's row 5 was, since `fakeSelectedItems` still holds the previous page's ids.

Two designs were tried and rejected (2026-08-30); do not re-derive them:

- **`position: sticky` on the spinner**, to keep it in view over a long table, cannot work. The icon's containing block *is* the mask it would need to escape, and in the app the wrapper's `overflow-auto` makes it a scrollport that never scrolls, so the offset resolves to zero. A Karma pin would still pass, because Tailwind is not loaded there — a test asserting a behaviour the app does not have. The mixin anchors the spinner with `align-items: flex-start` instead.
- **A CSS `animation-delay` anti-flash veil** was dropped rather than shipped. It splits the clock — the delay covers only the mask, while the empty-message suppression flips instantly — and any `prefers-reduced-motion` reset silently deletes it. No debounce ships in v1: the flash was objectionable because the affordance was a black scrim, and with a low-contrast veil it may not be perceptible at all. Add one only if a human reports flicker in the live admin.

**Known follow-up, not fixed here (2026-08-30):** the `lazyLoad` subscription is unmanaged — no `switchMap`, no `takeUntil(destroy$)` — so pending is a boolean rather than a property of an identified in-flight request. Two overlapping refetches (plausible with `[filterDelay]="500"` plus a page click) let the first response lower the flag while the second is still outstanding, and they can land out of order. Pre-existing, but this work is what made `loading` load-bearing for what the reader sees. A `switchMap` over a subject fixes ordering, cancellation and the leak together. (The bar's Clear used to make this concrete — `clear(table)` fired the store's requery fetch AND `table.clear()`'s own lazy load, unsequenced; that pair died 2026-09-06 when Clear narrowed to the store alone. The unmanaged subscription itself remains, and `[filterDelay]` + page-click overlaps still need the `switchMap`.)

Spec note: any test that drives a refetch must do it inside `fixture.ngZone.run(...)` (why: the doc comment on `swapRowNameTo`); three older specs call `table._filter()` unwrapped and are exposed to it.

## State persistence

Every persisted key is a getter on the component (`filtersStateKey`, `viewStateKey`, `columnsStateKey`, `layoutStateKey`, `viewSortStateKey`), each doc'd with its storage and why; PrimeNG's own sort and page state sits under `resolvedStateKey`, derived in `ngOnInit`, and the bar's Clear filters reaches only the store (the doc comment on `clear()`). **The view segment is on the FILTER key too, not just the layout keys** (decision 10 — a view IS a saved question).

**The active view survives a reload (2026-09-05).** Why its key is neither view-scoped nor always-local: the doc comment on `viewStateKey`; the reload gap it closed: the one on `restoreActiveView`. Rules, each spec-pinned in the "active view survives a reload" suite:

- **Only a view the change BROUGHT is adopted (2026-10-11, spiderly#430)** — view counts made consumers rebuild `[views]` on every list load; why that broke the re-read-every-change rule is on `adoptStoredView`. Spec: "a view tab shows its count".
- A restored TRANSIENT view re-derives its question, and a cleared non-transient one stays empty on F5: both rules live once, in `enterViewFilters`, which `viewFilterPayload` also enters into a scratch store (2026-10-12, spiderly#439: a view's count has to honor the filters saved on it). Spec: "a view's filter payload".

## Per-cell click — `Column.onCellClick`

Its contract is the doc comments on `Column.onCellClick`, `CellClickEvent` and the component's `onCellClick`: the affordance, display cells only, the swallowed row navigation, and why `element` is captured at dispatch.

Consumer usage — anchoring a popover, including the re-anchor-while-open pattern — is documented once in the agent bundle: `claude-plugins/docs/angular-customization/index.md` → `onCellClick`. Don't duplicate the snippet here (the duplicated `show()` example was how a re-anchor bug propagated to a consumer).

## Per-cell templates — `<ng-template spiderlyCellTemplate="field">`

Every editing note for this seam is on its code: `SpiderlyCellTemplateDirective` and `CellTemplateContext` (the context, and the typing in `ngTemplateContextGuard`), `getCellTemplate` (the live QueryList, actions columns never matching), and the comment above `#defaultCell` (one context for both branches).

## Column widths — `table-layout: fixed` and `Column.width`

Why the defaults are generous, and why they are a width rather than a minimum: the `Column.width` doc comment and `tableStyle`.

Editing notes:

- **`width: 0rem` is not "shrink to content" any more.** It meant that under auto layout; fixed layout reads it literally and the column vanishes. Two sites had it and both now carry a real width — the selection `<th>` in the template, and the fallthrough in `getColWidth` (actions columns, sized from `actions.length`, against a gap set beside them in the template).
- **The table needs no `min-width` of its own** — don't add a computed floor to `tableStyle`. The browser already widens a table past its container to the sum of its declared column widths, so `overflow-auto` still scrolls; the "too wide for its container scrolls" spec holds the measurement.
- **The library styles `#defaultCell` directly; a projected template is reached through custom properties, never selectors.** `.cell-text` is authored in this template, so a plain `:host` rule matches it. A consumer's `spiderlyCellTemplate` renders markup carrying the CONSUMER's `_ngcontent` and is unreachable from here by selector — don't reach for `::ng-deep`. What DOES cross that boundary is inheritance: `td.cell-wrap` publishes the `--spiderly-cell-*` properties (see the SCSS), the default cell's own clamp reads them with the clamp as fallback, and a consumer clamp written the same way follows the operator's wrap toggle for free (consumer-facing recipe: `claude-plugins/docs/angular-customization/index.md` → column widths). Before this contract the toggle was a silent no-op on any templated column — every visible column of the PACMS orders grid — while the menu showed a checkmark.

## The e2e fixture drives the bar, and must move with it

`tests/e2e-fixtures/frontend/tests/e2e/page-objects/base-page.ts` holds the filter helpers (`applyTextFilter` / `applyNumericFilter` / `applyBooleanFilter` / `clearTableFilters`), whose comments name the bar's `data-testid`s and why each commit awaits its own list response. Changing the bar's DOM, the editor's commit flow or the persistence keys changes those helpers and `specs/product-crud.spec.ts`'s storage asserts too — the stale-helper failure mode is a 30s Playwright timeout on an element that no longer exists (it happened to the old header-filter helper on CI run 33279746074).
