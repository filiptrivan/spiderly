# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

## Operator-owned view — the rework decided 2026-09-02 (Filip, grilled)

**Status: DESIGN, not shipped.** Every section below this one describes what the component does
TODAY. This block is the ONE telling of what replaces them and why; the affected sections carry a
pointer back here rather than a copy.

**The premise that broke.** Every presentational knob on this grid is set by whoever writes
`Column[]`: which columns show, their width, their order, whether a value is clamped — and, because
`clearHiddenColumnConstraints` drops a hidden column's constraints, which filters an operator may
even use. The operator gets no say. Five complaints landed in one sitting against the PACMS orders
grid, and they are one feature rather than five: **presentation belongs to the operator, the data
contract stays with the developer.**

Decisions, in the order they were resolved. Do not re-derive them. **Decisions 1 and 2 were
rewritten later the same day**, after Filip proposed the filter engine below; the earlier form
kept filters as a property of `Column` behind a `filterSurface` flag, and is in this file's git
history only.

**1. A filter is a FIRST-CLASS ENTITY, and the engine knows nothing about columns.** Filter state is
a normalised `{ filterId, operator, value }` set in a store the CONSUMER owns and hands to the table
(`[filters]="orderFilters"`), the ownership shape Angular Material's `MatTableDataSource` and
TanStack Table already use. The engine does three things: validate, serialise to the paginator query
(or apply predicates client-side), emit changes. A column REFERENCES a filter with `filterId?`, so
one definition serves every surface with no branching.

- `filterId` is typed as `keyof` the store's declaration, never a bare `string`. `Column.field` is
  already `string & keyof T` and fails the build on a typo; agents will migrate 195 declarations
  across 27 files, where a silent name miss is the worst failure mode on offer.
- **`Column.filterId` is NOT built yet, deliberately (2026-09-04).** With the bar owning every
  filter, no column references one: the first migration (PACMS `tag-list`) declares its store
  beside its columns and needs no link between them. `filterId` becomes necessary only when a
  control is placed back into a header cell, i.e. with `spiderlyFilterTemplate`. Build it then.
- **What `Column.filterType` means has split, and nothing renames it.** On a table WITH a store it
  is only the column's value shape, feeding `getColWidth` and the blob/value cell branch; on a
  table without one it additionally renders the header filter. Renaming it would be the 195-line
  migration decision 2 exists to avoid, so it keeps its name and loses a job.
- **The schema is DERIVED from what the generated paginator implements, not hand-written.** This is
  the one gap in the sketch, and it has already bitten twice by hand: `paymentGatewayCode` is `text`
  rather than `multiselect` because the paginator answers `In` on a string column with
  `InvalidMatchMode`, and `lastCommentText` is `sortable: false` because sorting a projection is a
  400. Today a human not declaring the wrong thing is the only guard; an engine that assembles
  operators itself removes that human, so the per-type operator set has to come from the generator.
- `filterField` disappears with this. It exists today only as the multiautocomplete patch
  (`filterKey(col)` = `filterField ?? field`), i.e. as the coupling already cracking.
- Sort needs no equivalent and must not be re-coupled: `multiSortMeta` is keyed by `field`, so the
  bar's sort chip already works without any notion of a column.

**2. There is NO inline/toolbar MODE, because placement is a slot.** `*spiderlyFilter="'paymentMethod';
let f"` renders that filter's control wherever it is put, typed through `ngTemplateContextGuard`
exactly as `spiderlyCellTemplate` already is. Put it in a header cell and you have header filtering;
put it in a drawer, a modal, or a sidebar and that works too, with no library change — which is the
whole reason `mode: 'toolbar' | 'inline'` was rejected. The store being consumer-owned rather than
table-provided is what makes the out-of-tree cases (a modal rendered at app root) work without a
second API, so the injectable path is equal, not a fallback: documented and tested alike.

- A filter that projects no template gets the DEFAULT control for its type in the chip bar, so ~90%
  of filters are one line in the store declaration and nothing in any template. That is what the
  generator scaffolds, and it is what keeps a generated table working with zero consumer TS.
- **No `filterSurface` flag ships, and no deprecation window either.** Spiderly has no external
  consumers (Filip, 2026-09-03), so the Angular-aligned deprecate-in-N / remove-in-N+1 window this
  record first prescribed buys nothing: the legacy `p-columnFilter` header path is deleted in the
  same 19.12.0 that adds the engine. The constraint that forced that window is still true and worth
  knowing when a future breaking change comes up — Spiderly's major tracks Angular's (19.11.9, peer
  `@angular/core ^19.2.0`), so our own breaking changes have no major slot to land in.
- **What keeps the 27-table migration incremental is the SHAPE of the input, not a flag.** A table
  handed a filter store renders the bar; a table handed none keeps its `Column.filterType` header
  filters. PACMS then migrates a table at a time against one published version, and the legacy path
  is deleted once nothing passes the old shape — self-removing, with no branch anyone must remember
  to flip. A big-bang migration was rejected because this is NOT mechanical work: each table needs
  the filter-only / filter-mostly / display split made by judgement, and that has so far been done
  for exactly one of the 27 (PACMS `order-list.component.ts` -> `buildColumns`).

**2b. What the chip bar carries**, unchanged from the first form of this record: a chip per applied
filter, one chip for the multi-sort with its priority spelled out, and a result count
(`812 of 71,629`). Consequences that licence the rest of this document:

- A hidden column may KEEP its constraint, because the chip is now the visible surface.
  `clearHiddenColumnConstraints`, `reconcileVisibilityWithPersistedConstraints` and
  `defaultMultiSortMeta`'s hidden-column null all go.
- `snapshotAppliedFilters` / `appliedFilterKeys` and the projected `filtericon` go with them: a chip
  cannot claim a filter that is not applied, so the icon that could is unnecessary. Same for
  `hideOnClear` and `filterAppliesOnChange`.
- `DEFAULT_COLUMN_WIDTH_REM` is re-derived from VALUES. Today's `text: 12` / `multiselect: 12` /
  `numeric: 12` are sized for a filter input plus a match-mode dropdown; with the input gone a status
  chip wants ~7rem and a "Da"/"Ne" ~3rem. That reclaimed width is half the answer to decision 9.

**3. A `▾` menu on every column header** carries: sort asc/desc, `Filter…` (writes into the bar),
move left / move right, fit width, wrap text, hide column. Hiding from the column itself is the
point; the chooser popover keeps only the jobs a header cannot do, i.e. revealing a hidden column
and the reset.

**4. Widths are MINIMUMS, and a drag sets one.** One rule in both regimes: when the minimums fit, the
surplus is shared in proportion (today's fluid behaviour, worth keeping on a 2560px monitor); when
they do not, the table takes its natural width and the wrapper scrolls. That horizontal scroll
already exists and is spec-pinned (the "a table too wide for its container scrolls" suite), so this
is not a new capability, only the acknowledgement that a 12-column grid cannot and should not fit.

**5. PrimeNG's resize/reorder STATE is unusable here; only its gestures are.** Measured in
`primeng-table.mjs` 19.1.3: `saveColumnWidths` stores `widths.join(',')` and `restoreColumnWidths`
replays them through `th:nth-child(n)` — index-keyed, so one chooser toggle slides every stored width
onto the wrong column, and so does adding a column in a later release. `restoreColumnOrder()` is
commented out of `restoreState()` outright. `onColumnDrop` reorders `this.columns`, an input this
component never binds. So widths, order and wrap persist by `field`, in our own store beside
`columnVisibilityOverrides`.

**6. Header drag-to-reorder ships, and needs OUR OWN directive.** The sort conflict does not exist:
`pSortableColumn` listens on `click`, `pReorderableColumn` sets `draggable` on `mousedown` and goes
through native HTML5 drag, and the browser suppresses the click when a drag actually happens (a drag
released on its own column hits `dragIndex == dropIndex` and does nothing). Two things PrimeNG's
directive does not handle, which is why we write our own: its `onMouseDown` excludes only `INPUT`,
`TEXTAREA` and `.p-datatable-column-resizer`, so pressing our `▾` button would start a column drag
instead of opening its menu; and `onColumnDrop` reorders blind, with no notion of decision 8's frozen
columns. Drag is for neighbours — HTML5 dnd has no edge auto-scroll, so long moves belong in the
chooser list, and the menu's move left/right is the only keyboard path.

**7. Revealing a column does NOT append it.** A known column returns to its stored position, because
hide→show must be an identity operation: a chooser you cannot experiment in is worse than a column in
an awkward place. A column the stored order has never seen (added in a later release) DOES go to the
end, so a new field never displaces a layout a person built. The complaint behind "append it" was
really "I ticked it and saw nothing happen", answered instead by making the chooser a drag-ordered
list of checkboxes (the position is visible at the moment you tick) and by scrolling the revealed
column into view with a brief highlight.

**8. The left edge freezes: the `lockVisible` column plus the selection column,** `position: sticky`
with an edge shadow. Once the grid scrolls horizontally a row loses its identity, and NN/g's *Data
Tables* is explicit that the leftmost header column must lock in place. `lockVisible` already names
exactly the right column in every table, so this needs no new API. Costs to plan for: a sticky cell
needs an opaque background that tracks row hover and striping, and the second frozen column's offset
needs the first one's measured px width.

**9. Truncation stays the default; what changes is that it stops being SILENT.** Flipping to wrap was
considered and rejected: under `table-layout: fixed` a wrapped value grows the ROW, and one free-text
column turns 25 rows into a saw edge — the cost `.cell-text`'s own comment already spells out.
Verified 2026-09-02 against the tools we measure against: Airtable's grid defaults to "Short", one
line, truncated, with row height as a view-bar control; Notion's table truncates and puts `Wrap text`
in the per-column header menu, the same place decision 3 puts it. What WAS broken is that
`#defaultCell` sets no `title`, so a clamped value had no recovery path at all — which is why PACMS
hand-added `[title]` in four places. Fix: a directive setting a native `title` only when
`scrollWidth > clientWidth` (a `ResizeObserver`; not a blanket title, because a tooltip on a cell
that fits is noise, and `pTooltip` was already rejected for firing as the cursor crosses a dense
list).

**10. Layout is scoped to the VIEW, not the table.** Built-in views ship in consumer code and carry
columns + widths + order + filters + sort; "all rows" is simply the default view. A personal override
is a delta per view, so the storage key gains a view segment. The alternative (layout global, a view
carrying only filters and sort) was rejected because it re-creates the original problem one level up:
a picking view and a payments view want different COLUMNS, not just different rows.

**What has shipped, as of 2026-09-05.** All five complaints are answered; the record above is
still the design, and this is where it stands against it.

- **Done:** the filter engine and chip bar (decisions 1, 2, 2b) with text/number/boolean/date
  controls and a pick-list; the result count and sort chip; `+ Filter` as a searchable popover;
  a column header menu carrying Hide, Wrap and Move left/right; per-column wrap, order and width
  persisted by `field` under `${resolvedStateKey}:layout` (decisions 5, 7); resize by dragging a
  header edge, trading share with the neighbour (decision 4); `resetColumnLayout` undoing all of
  it. PACMS `tag-list` is the first table on the bar.
  The header menu is complete at six items: `Filter…`, sort ascending/descending, move
  left/right, fit width, wrap text, hide column. `Column.filterId` exists now — decision 1 left
  it unbuilt until something asked, and `Filter…` asked. It is a LINK, not a declaration, and it
  is untyped against the store on purpose: a `Column<T>` knows its row type and nothing about
  which store it renders beside, so the compile-time key check stays at `createFilterStore`.
  Decision 9's `title`-on-overflow directive shipped too (`SpiderlyOverflowTitleDirective`).
  Header drag (decision 6), the frozen left edge (decision 8), `spiderlyFilterTemplate` and
  views (decision 10) all shipped too — every decision in this record is now built.
- **What is left is CONSUMER work, not library work:** PACMS has ONE of 27 tables migrated
  (`tag-list`, which carries filters, `filterId` links and three views). Orders is the one the
  whole rework was argued from and it is untouched: nineteen columns, four two-line cell
  templates, and the filter-only / display split already written out above `buildColumns`.
- **Custom views were asked for and PARKED (Filip, 2026-09-05).** Views ship declared in consumer
  code; an operator cannot save their own. Filip asked for that after using the tags grid, then
  agreed to wait for a signal rather than build it — Plaky "Waiting" 7387519 carries the full
  reasoning and what would have to happen. Do not build it on a hunch: if it lands, it needs
  saving a named snapshot, a list mixing built-in and personal, deletion, and a name-collision
  rule — and if views must be SHARED between operators it stops being localStorage and becomes a
  table, a CRUD surface and permissions.
- **Two rules a later table must not re-derive:** a view CLEARS before it applies, so two never
  compose; and both layout keys carry the active view, so a table with no views reads exactly the
  keys it already wrote — which is what keeps the twenty-six unmigrated grids working untouched.

**Three more traps, all found by a spec and none visible by eye:**

- **`ResizeObserver`'s first callback lands an animation frame after paint.** The frozen column
  sat at `left: 0`, on top of the checkbox column it starts after, for that frame — and its own
  spec had been passing on timing luck until adding suites reordered the run. Measure once on a
  microtask as well; writing it inline in `ngAfterViewInit` is an NG0100.
- **A `once` listener on `document` outlives the gesture that armed it.** The click-swallower
  that stops a resize from also sorting ate an unrelated spec's first click, and would eat an
  operator's next click after any drag that ends without one. Remove it on the next tick.
- **PrimeNG's `p-inputtext-sm` does nothing on a `p-select`.** Every component has its own small
  class, so size controls through the component's `size` input, never a borrowed class.

**Two arithmetic traps in the width model, both found by a spec and neither visible by eye:**

- **Scaling a share by `needed / current` does not fit the column.** Shares are a proportion of
  whatever width the table has, so growing one shrinks every other column's realized width and
  the naive figure lands short — the cell stayed clipped after being told to fit. `shareThatFits`
  solves the proportion, and falls through to a straight px-to-rem conversion once `needed` is
  the whole container, where the table overruns and a share is its own length again.
- **Sorting hangs off CLICK, which `stopPropagation` on `mousedown` never touches.** Every
  resize also reordered the grid. Swallowing the click on the grip is not enough either: a drag
  ending away from it fires the click on their common ancestor, the `th`. One capturing listener
  on the document, removed on the next tick — left to `once` it sat there and ate an unrelated
  spec's first click.

**Two traps this component now has, both cost an afternoon each:**

- **The template is a JS template literal, so a backtick anywhere in it — including inside an HTML
  comment — terminates it**, and TypeScript reports the error lines away from the cause. Twice.
- **PrimeNG does not re-project content an `@if` inside a popover has destroyed.** Reopening gives
  a container with `overlayVisible` true and nothing in it. Render the menu items once and hold
  the target in a field (`menuColumn`); never key the popover's content on the thing it acts on.
  A template reference variable also does not cross an `ng-template` boundary, so the header's
  button reaches the caption's popover through a `viewChild`, not through `#ref`.

**Rollout, three waves, each a spiderly release plus a PACMS upgrade.** (1) The filter engine, the
slot directive and the chip bar — decisions 1, 2 and 2b, with 9's `title` directive as a rider, and
the 27 PACMS tables migrated one at a time across it. (2) Header menu, chooser drag list, own reorder directive,
field-keyed width/order/wrap persistence, frozen edge — decisions 3 to 9. (3) Views — decision 10.
**Wave 3 is not optional.** Without it, waves 1 and 2 are a pile of knobs an operator re-sets by hand
every morning, and the smaller honest plan (the `title` directive, a two-item header menu, and a bare
"N filters on hidden columns · Show" strip instead of the bar) would have been the better trade.
Filip took the full plan on that condition.

## Custom toolbar actions — `<ng-template spiderlyDataTableActions>`

Consumers add their own toolbar buttons/markup by projecting an `<ng-template spiderlyDataTableActions>` (the `SpiderlyDataTableActionsDirective` marker). The component picks it up via `@ContentChild(SpiderlyDataTableActionsDirective, { read: TemplateRef })` and renders it with `*ngTemplateOutlet` at the **start** of the caption's right-side action row — `*ngIf`-guarded so an un-projected slot renders nothing (no stray flex gap).

- **Rendered before the built-ins on purpose.** Delete Selected is conditional, so trailing custom buttons would shift when selection toggles. Leading keeps them positionally stable. If you reorder the caption, keep the outlet first.
- **No context is passed** to the template (it binds to the consumer's component). This is deliberate: lazy-load selection has no clean flat-id representation (`newlySelectedItems`/`unselectedItems` under select-all). If a future need appears, add `ngTemplateOutletContext` keys — that's non-breaking, existing templates ignore unknown `let-` vars.
- The contract is covered by `spiderly-data-table.component.spec.ts` (the library's TestBed suite; runs via the `Unit Tests (Angular)` CI job, `karma.conf.js` → `ChromeHeadlessNoSandbox`).

## Shift-click range selection — anchor model

Selection checkboxes support Gmail-style ranges: shift+click applies the clicked checkbox's new state to every row between it and the anchor (`rangeAnchorId`). Consumer-facing behavior: `claude-plugins/docs/angular-customization/index.md` → "Row selection". Editing notes:

- **The shift state lives on the mousedown, not on the checkbox event.** `p-checkbox` emits the DOM `change` event as `onChange.originalEvent`, and `change` carries no `shiftKey` — so `onSelectionCellMouseDown` captures it into `pendingShift`, which the same click's `selectRow` consumes-and-clears. Don't "simplify" back to reading the event.
  - **Arming is scoped to a press that will actually produce a `change`**: only a press landing inside the `p-checkbox` arms (a press on the surrounding cell produces no change, so it would strand the flag until some later toggle consumed it), and the flag is **id-paired** so a press on one row can't range a different row's toggle. **A clear on the cell's `click` does NOT work** and was tried: the checkbox's `change` fires *after* click dispatch completes, so a click-time clear runs first and kills every real range (it reds the whole range suite — that failure is the pin). The residual, accepted: press the checkbox, drag off, release outside, then toggle that same row by keyboard — it ranges. Closing it needs a document-level `mouseup`, which has the same ordering problem.
  - The `preventDefault` that suppresses text selection still covers the whole cell, independent of arming.
- **Ranges resolve over `renderedRows()` — the display window — never over raw `items` or `rowData.index`.** It delegates to PrimeNG's own `table.dataToRender(null)` (`filteredValue || value`, sliced by the paginator, lazy capped at `rows`) precisely so it cannot drift from what the table paints; hand-rolling the slice re-introduced a gap where a server overshooting `Rows` would let a range sweep unrendered rows. An `items`-based range would sweep filtered-out or off-page rows sitting between two visually adjacent ones (`rowData.index` can't address a range at all — only `loadFormArrayItems` stamps it). The invariant is validate-at-use: an anchor outside the window misses `findIndex` and the shift-click degrades to a plain toggle, so page flips and client-side sort/filter need no per-path resets. The only explicit resets are where rows are wholesale replaced — `lazyLoad` (the decided reset-on-reload semantics for lazy tables) and `loadFormArrayItems`.
  - **`rangeAnchorId == null` is the no-anchor sentinel, so a null id must never resolve to a row** — client-side tables can hold unsaved rows whose `idField` is null, and `findIndex` would match the first of them. `selectRow` guards both the anchor and the clicked id explicitly.
- **Every change goes through `toggleRow`** (the former `selectRow` body): that is what keeps the lazy delta model (`newlySelectedItems`/`unselectedItems`) and per-row event emissions identical to manual clicks. Rows already in the target state are skipped — the skip is what prevents duplicate ids in the delta arrays.
- The selection `<td>` suppresses the browser text selection at shift-mousedown, with `user-select: none` as belt. It no longer stops click propagation — see the arbitration seam below.

## Row navigation vs interactive cells — one seam, `.row-interactive`

`onRowClick` decides whether a click navigates: anything under `.row-interactive` owns its own click and never navigates. **Mark a new interactive cell with that class; do not add another `stopPropagation`.** Per-surface stops were the old mechanism and were structurally lossy — each surface had to remember, and the actions column never did, so on a `navigateOnRowClick` table a Delete click opened the confirm dialog *and* navigated away. Carriers today: the selection cell, each action `<span>`, editable cells, and opted-in `onCellClick` cells.

- **The marker sits on the action `<span>`s, not their flex container** — that container renders in *every* non-actions cell too (empty), so marking it would have made a strip of every ordinary cell non-navigating.
- `onCellClick` keeps its own `stopPropagation` (documented consumer contract, and it also shields consumers' outer handlers); it is belt now, not the mechanism.
- `onRowClick` resolves the row id through `idField`, not a hardcoded `id` — a table keyed by anything else silently never navigated at all.

## `showMatchModes` defaults to false on every column

The match-mode `<p-select>` rendered next to text/numeric/date filter inputs is gated by **two** conditions in PrimeNG (`*ngIf="showMatchModes && matchModes"`). Spiderly always supplies `matchModeOptions` (`matchModeNumberOptions`, `matchModeDateOptions`), so the second condition is satisfied — but the binding `[showMatchModes]="col.showMatchModes"` resolves to `undefined` when a column omits the flag, and PrimeNG's `booleanAttribute` coerces that to `false`. Net effect: the dropdown does not render and the column filters with the default match mode (`Equals` for numeric, `Contains` for text) only.

To let the user pick a match mode, set `showMatchModes: true` on the column. Example:

```typescript
{ name: t('CreatedAt'), filterType: 'date', field: 'createdAt', showMatchModes: true }
```

## Match-mode labels are runtime translations

`matchModeNumberOptions` / `matchModeDateOptions` populate `label` from `translocoService.translate(...)`, so the user-visible option text is the **value** in `assets/i18n/<locale>.json`, not the key. For English:

| `MatchModeCodes` | translation key | rendered label |
|---|---|---|
| `Equals` | `Equals` | `Equals` |
| `LessThan` | `LessThan` | `Less than` |
| `GreaterThan` | `MoreThan` | `More than` |
| (date) | `OnDate` | `On date` |
| (date) | `DatesBefore` | `Dates before` |
| (date) | `DatesAfter` | `Dates after` |

When matching options programmatically (e2e tests, conditional logic), match against the rendered label, not the key. Renaming the key without updating en.json or vice versa silently breaks consumers that match by label.

## Column chooser — `Column.visible` / `Column.lockVisible`

**Superseded in part by "Operator-owned view" above** (2026-09-02): the hidden-column invariant
below, and the v1-scope line deferring reordering and width persistence.

Consumer-facing behavior is documented in `claude-plugins/docs/angular-customization/index.md` → "Column chooser". Editing notes:

- **The invariant everything serves: a hidden column contributes nothing to filtering or sorting.** The header is the only filter surface, so a kept constraint would restrict data invisibly. Three enforcement points, all must stay aligned: `clearHiddenColumnConstraints()` (on hide), `reconcileVisibilityWithPersistedConstraints()` (on init, against state this component didn't write), and `defaultMultiSortMeta()` returning null when its column is hidden (otherwise `applyDefaultSortIfUnsorted` would re-add an invisible sort right after clear-on-hide removed it).
- Template loops iterate `visibleCols` (recomputed via `refreshVisibleCols()`), never `cols`. Actions columns (no `field`) always render and never appear in `chooserCols`.
- Overrides live in `columnVisibilityOverrides` — **only fields the user explicitly toggled off their declared default** (toggling back to default deletes the entry). Persisted to `` `${resolvedStateKey}:columns` `` in **localStorage always** (column layout is a durable preference), unlike the filter state which follows `stateStorage`. `lockVisible` beats a stale persisted override. Reconciliation reveals go in the separate transient `revealedByConstraint` set (never persisted — keeping them out of the override map is what stops a later toggle's persist from promoting a safety reveal into a durable choice; an explicit toggle on that column supersedes the reveal).
- Filter state is keyed by `filterField ?? field`; sort meta by `field`. The filter half of that rule has ONE home — the `filterKey` method, which the template's `[field]` binding, `isColumnFiltered`, `clearHiddenColumnConstraints` and the reconciliation all share. Never inline the expression again; sort stays on bare `field`.
- v1 scope: the chooser only renders for lazy tables (`hasLazyLoad`); client-side form-array tables keep their declared columns. Reordering/width persistence deliberately out of scope.
- Spec gotcha: the chooser checkboxes' `[ngModel]` writes resolve in a microtask — tests must `await fixture.whenStable()` after opening/toggling (see `openChooser` in the spec). Once open, PrimeNG appends the popover to `document.body`, so specs query the `Popover` instance's `container`, never the document (stale popovers from earlier fixtures linger there).
- That teleport also means chooser styles are declared at SCSS **top level**, never under `:host` (see `Angular/CLAUDE.md` → overlay styling); every chooser rule gets a row in the spec's `stylePins` table.

## Rows-per-page — `rowsPerPageOptions`

Default `[10, 25, 50, 100]`; `ngOnInit` merges **both** the effective initial `rows` **and** the persisted pick (`persistedTableState()?.rows` — PrimeNG's `restoreState` will overwrite `rows` with it after init) into the list when missing — either value outside the options leaves PrimeNG's paginator dropdown blank. The merge must run after `resolvedStateKey` is derived, or the persisted read is a silent null. The user's pick persists for free through PrimeNG table state (`saveState`/`restoreState` carry `rows`) — do NOT add custom persistence; a durable (localStorage) page size was considered and deliberately deferred (2026-08-17) until usage asks for it. The 100 ceiling is a UI courtesy only: the backend `.Take(filterDTO.Rows)`s whatever it is sent, uncapped.

## Scroll back to the table on page change — `onPageChange`

Paging leaves the reader parked at the paginator with the new rows off-screen above it, so `onPageChange` puts the wrapper `<div>` back under the top of the viewport. Unconditional, with no opt-out `@Input`: PrimeNG holds the same policy for `[scrollable]` tables (`Table.onPageChange` ends in `resetScrollTop()`) and offers no flag either. That branch never runs for us because we drive `responsiveLayout="scroll"`, not `scrollable` — the window is what scrolls here, not a container inside the table.

- **The hook is `(onPage)`, not `lazyLoad`.** `Paginator.onRppChange` routes the page-size dropdown through `changePage()` → `Table.onPageChange` → `onPage`, so one binding covers page navigation *and* page size, on lazy and client-side tables alike. `lazyLoad` also fires for sort and filter, where the user is already up at the header. **`restoreState` never emits `onPage`** — it assigns `first`/`rows` directly and emits only `firstChange` — which is why a restored page offset does not scroll on first paint. That is spec-pinned, because a scroll there would shove a details page's form off-screen the moment it opens.
- **The target is the `#tableContainer` wrapper, not the host.** `:host` declares no `display`, so `<spiderly-data-table>` is `display: inline` and its `getBoundingClientRect()` is not a dependable box for the "is it above the viewport" test. The template hands the element straight to the handler — `(onPage)="onPageChange(tableContainer)"` — so there is no `@ViewChild` and no nullable plumbing field on the component's public surface.
- **The guard is an explicit `rect.top < scrollMarginTop`, not `block: 'nearest'`.** `'nearest'` looks like it would skip a needless scroll for free, but its behaviour when the element overruns the viewport at both edges is the case we could not confirm against CSSOM-View (both the W3C and csswg copies truncate before the algorithm, checked 2026-08-29). The explicit check is two lines and testable. It is also what keeps a table rendered *below* other content — `UIControlTypeCodes.Table` inside a generated details page — from yanking that content away when everything was already visible.
- **The offset lives in CSS and is read back from CSS.** `.spiderly-table-container` gets it from the `scroll-target-below-top-chrome` mixin (`styles/layout/_mixins.scss`), which is the one declaration both this component and `spiderly-data-view` include; the var it reads belongs to the layout — `Angular/CLAUDE.md` → fixed-chrome offset. Why `behavior: 'instant'` and why the offset is read rather than passed: the comment block on `scrollElementIntoViewIfAboveViewport`, which is the single telling.
- **`spiderly-data-view` shares the helper and the mixin**, having had the identical wrapper and the identical bug. Its scroll binding is still not covered — every spec here mounts `SpiderlyDataTableComponent` — but the component does have a spec file now (`spiderly-data-view.component.spec.ts`, added 2026-08-30 with the pending-state work), so adding a scroll case there is cheap when someone wants it.

## Pending state — one predicate, stale rows, and a veil

Every refetch raises `loading` and leaves `items` alone: the previous page stays readable under PrimeNG's overlay rather than the table blanking. Before 2026-08-30 `lazyLoad` never raised the flag at all — only `reload()` did — so paging, sorting and filtering ran with no feedback whatsoever, which is invisible on a fast query and glaring on a slow one.

- **The bug was worse than a missing spinner: the table asserted something false.** PrimeNG gates its empty message on `isEmpty() && !loading`, so a table whose last result was empty answered "no records found" for the whole of the next request, then flipped to rows. That is the spec worth keeping red in your head — a pending signal is what licenses keeping stale rows, and without it the two failure modes compound.
- **`isPending` is the single predicate**, feeding PrimeNG's `[loading]` *and* the container's `[attr.aria-busy]`, so the visible state and the exposed busy state cannot disagree. **That is not a11y parity**: `aria-busy` defers announcements, it does not produce one, and a screen-reader user is still told nothing when a page flip starts or lands. There is no live region to announce into — zero `aria-live` in this library and none in PrimeNG 19.1.3's table or paginator (checked 2026-08-30). Adding one is a library-wide decision needing an `sr-only` utility and a seeded Transloco key, not a rider on this. There is deliberately no opt-out `@Input`.
- **`isPending` returns `this.loading` and nothing else.** It briefly also tested `items === undefined`, which is dead once `loading` initialises true and every `lazyLoad` raises it — dead everywhere except a **failed** first load, where the flag drops with `items` still undefined and the overlay was pinned up forever. Do not re-add the clause.
- **There is no `loadingbody` template, on purpose.** PrimeNG renders it *in addition to* the rows, never instead of them, so it hung a "Loading..." row under the stale page. It was deleted rather than `*ngIf`-guarded — with the overlay up it added nothing, the data view never had one, and it carried an `[attr.colspan]` that had to track `visibleCols` plus the selection column forever.
- **The overlay's styling is the `table-pending-veil` mixin** (`styles/layout/_mixins.scss`), shared with `spiderly-data-view` and pinned once by `expectPendingVeil` in `testing/spec-support.spec.ts`. Why PrimeNG's own scrim had to go, why `color` is set alongside the background, and why the spinner is top-anchored: the mixin's doc comment, which is the single telling.
- **`reload()` is not special.** It replays the last lazy-load event and nothing else. It used to null `items`; that never drove the overlay (the predicate tests `items === undefined`, and `null` is not `undefined`) and only ever drove `isEmpty()`, which `loading` already suppresses.
- **The selected-ids request is issued alongside the list request, not after it.** It needs only `tableFilter`, so awaiting it behind the list response cost a second round trip on every page of every selection-enabled table — and, now that the flag covers refetches, held the overlay up for both. The `.catch` attached at creation is mandatory: when the list request errors, `next` never runs, so an uncaught eager promise would be exactly the unowned rejection the `finally` below exists to prevent. It is rethrown inside `reconcileSelectionForLoadedPage`, where the `catch` owns it.
- **The selection await is wrapped in `try/finally`.** `lazyLoad`'s `next` is `async` and awaits `selectedLazyLoadObservableMethod` before lowering the flag; a rejection there settles nothing, and the subscriber's `error:` belongs to the *paginated-list* observable, so it never runs. That stranded the overlay forever. `loading = false` stays **after** the await deliberately: lowering it earlier paints page 2's row 5 as checked because page 1's row 5 was, since `fakeSelectedItems` still holds the previous page's ids.

Two designs were tried and rejected (2026-08-30); do not re-derive them:

- **`position: sticky` on the spinner**, to keep it in view over a long table, cannot work. The icon's containing block *is* the mask it would need to escape, and in the app the wrapper's `overflow-auto` makes it a scrollport that never scrolls, so the offset resolves to zero. A Karma pin would still pass, because Tailwind is not loaded there — a test asserting a behaviour the app does not have. The mixin anchors the spinner with `align-items: flex-start` instead.
- **A CSS `animation-delay` anti-flash veil** was dropped rather than shipped. It splits the clock — the delay covers only the mask, while the empty-message suppression flips instantly — and any `prefers-reduced-motion` reset silently deletes it. No debounce ships in v1: the flash was objectionable because the affordance was a black scrim, and with a low-contrast veil it may not be perceptible at all. Add one only if a human reports flicker in the live admin.

**Known follow-up, not fixed here (2026-08-30):** the `lazyLoad` subscription is unmanaged — no `switchMap`, no `takeUntil(destroy$)` — so pending is a boolean rather than a property of an identified in-flight request. Two overlapping refetches (plausible with `[filterDelay]="500"` plus a page click) let the first response lower the flag while the second is still outstanding, and they can land out of order. Pre-existing, but this work is what made `loading` load-bearing for what the reader sees. A `switchMap` over a subject fixes ordering, cancellation and the leak together.

Spec note: any test that drives a refetch must do it inside `fixture.ngZone.run(...)`, or the `delay(0)` is scheduled where `whenStable` will not wait and the assertion measures a still-pending table. That trap is documented once, on `swapRowNameTo`; three older specs call `table._filter()` unwrapped and are exposed to it.

## Filter-state persistence

**Gains a view segment under "Operator-owned view" above** (2026-09-02, decision 10): layout is
stored per view, not per table.

`@Input() stateKey?: string` plus `@Input() stateStorage: 'session' | 'local' = 'session'` light up PrimeNG's stateful-table behavior. When `hasLazyLoad` is true, `ngOnInit` derives `resolvedStateKey` from `router.url` (plus `additionalFilterIdLong` to disambiguate parent-child views). Consumers don't normally pass `stateKey` — leave it auto-derived. The `clear(table)` method also calls `table.clearState()` so the "Clear all filters" caption button wipes the persisted state instead of just resetting the in-memory table.

## Per-cell click — `Column.onCellClick`

Set `onCellClick?: (e: CellClickEvent) => void` on a column to make *its* value cells clickable (the mirror of `Action.onClick`, but for plain value cells rather than the actions column). Implementation notes for editing this component:

- **`td.clickable` is the affordance.** Opted-in cells get `cursor: pointer` + hover via the `td.clickable` rule in this component's SCSS — `.clickable` was previously declared on rows but never styled, so the rule is new.
- **Fires on display cells only** — text/numeric/date/boolean/`blob`. Editable cells (`col.editable`) are excluded; that cell belongs to its inline input.
- **Swallows row navigation.** The handler calls `event.stopPropagation()`, so on a `navigateOnRowClick` table an opted-in cell runs *its* handler instead of navigating. (On non-navigating tables this is a harmless no-op.)
- **`CellClickEvent` captures `element` synchronously on purpose.** It's the clicked `<td>`; we grab it at dispatch time because `originalEvent.currentTarget` nulls once dispatch ends, so it's already null by the time an async handler's HTTP response resolves. (`element` is a superset addition over `ActionClickEvent`'s, alongside `field`, `value` (raw) and `displayValue` (formatted).)

Consumer usage — anchoring a popover, including the re-anchor-while-open pattern — is documented once in the agent bundle: `claude-plugins/docs/angular-customization/index.md` → `onCellClick`. Don't duplicate the snippet here (the duplicated `show()` example was how a re-anchor bug propagated to a consumer).

## Per-cell templates — `<ng-template spiderlyCellTemplate="field">`

`SpiderlyCellTemplateDirective` carries the column's `field` as its input and the `TemplateRef` it sits on. The component collects them with `@ContentChildren`; `getCellTemplate(col)` scans that QueryList, and the cell falls back to `#defaultCell`, which holds the original blob/value rendering. Context semantics are documented once, on `CellTemplateContext`. Editing notes:

- **Read the QueryList live — don't cache it into a Map.** An earlier revision indexed by field in an `ngAfterContentInit` plus a `changes` subscription, purely so a consumer could declare templates inside a `@for`/`@if`. A `QueryList` already updates itself, so `find` over the (one or two) templated columns needs neither, and the lifecycle hook came back out.
- **Both templates get the SAME context object, `#defaultCell` included** — which is what keeps `getRowData` to one call per cell whichever branch renders, and stops the two branches from disagreeing about formatting.
- **Actions columns can never match** (`getCellTemplate` returns null without a `field`), so the actions `<div>` stays outside the outlet.
- **The typing lives in `ngTemplateContextGuard`, not in the injected `TemplateRef<C>`.** A directive's constructor type never reaches the template that declares it, so without the guard every `let-` var is `any` however well the context interface is written. `SpiderlyTemplateTypeDirective` is the same mechanism.

## Column widths — `table-layout: fixed` and `Column.width`

**Reworked by "Operator-owned view" above** (2026-09-02): widths become minimums a drag can set,
`DEFAULT_COLUMN_WIDTH_REM` is re-derived from values once the header carries no filter input, and
the missing `title` on `.cell-text` gets an overflow directive. Decisions 1, 4, 5 and 9.

`getColWidth(col)` takes the **column**, not the filter type — it needs `Column.width`. Why the defaults are generous, and why they are a width rather than a minimum: the `Column.width` doc comment and `tableStyle`.

Editing notes:

- **`width: 0rem` is not "shrink to content" any more.** It meant that under auto layout; fixed layout reads it literally and the column vanishes. Two sites had it and both now carry a real width — the selection `<th>` in the template, and the fallthrough in `getColWidth` (actions columns, sized from `actions.length`, against a gap set beside them in the template).
- **`DEFAULT_COLUMN_WIDTH_REM` is exhaustive on purpose.** It replaced a `switch` whose `default:` silently absorbed `blob`, sizing a 45px thumbnail's column at 2rem. A new `filterType` now fails the build there instead of inheriting the actions reservation.
- **The table needs no `min-width` of its own** — don't add a computed floor to `tableStyle`. The browser already widens a table past its container to the sum of its declared column widths, so `overflow-auto` still scrolls; the "too wide for its container scrolls" spec holds the measurement.
- **The clamp reaches `#defaultCell` only.** `.cell-text` is authored in this template, so a plain `:host` rule matches it. A consumer's `spiderlyCellTemplate` renders markup carrying the CONSUMER's `_ngcontent` and is unreachable from here by design — say so when a consumer reports a growing row, rather than reaching for `::ng-deep`.

## Active-filter header icon — the projected `filtericon` template

**Moot under `filterSurface: 'bar'`** — the chip bar replaces this whole apparatus. See
"Operator-owned view" above, decision 1.

PrimeNG 19's menu-mode filter button renders identically whether the column is filtered or not (its class is static, so there is no CSS-only fix), and the worst case is a `stateStorage`-restored filter on reload: the table opens filtered with zero signal. The projected `pTemplate="filtericon"` replaces PrimeNG's `<FilterIcon>` with `pi-filter` / `pi-filter-fill` + primary colour — the fill change is the non-colour channel (WCAG 1.4.1); colour only amplifies. Two traps, both spec-pinned:

- **The icon reads APPLIED state (`appliedFilterKeys`), never `table.filters` — this is the trap, and it shipped once.** `ColumnFilter.onModelChange` writes every keystroke of a text/numeric filter straight into the meta and calls `_filter()` only for the auto-applying types, so `table.filters` also holds constraints the operator is still typing. Reading it filled the icon on the first character, claiming the grid was narrowed while it still showed everything (Filip, 2026-08-29, on the live admin). `snapshotAppliedFilters` records the set at each COMMIT point instead — `(onFilter)` (which `_filter()` emits for lazy and client tables alike), `lazyLoad` (also the `table.clear()` path, which re-queries **without** emitting `onFilter`), the caption's Clear filters, and once in `ngOnInit` off persisted state so a restored filter is marked on first paint. The "typed but not yet applied" spec is the pin; note a spec that sets `filters` without calling `_filter()` now proves nothing.
- **Two questions, two predicates — don't collapse them.** `isColumnFiltered(col)` answers "are the rows on screen narrowed by this column" (the icon). `columnHasConstraint(table, col)` answers "does this column carry a constraint at all, pending included" — which is the right question for `clearHiddenColumnConstraints`, since a typed-but-unapplied value must still be dropped when the column is hidden.
- **Truth is our `isActiveFilterMeta`, never the template's `let-hasFilter` context.** PrimeNG's getter reads only the *first* constraint of array meta, so a multi-constraint column whose first slot is blanked but whose second still holds a value is genuinely filtered while `hasFilter` reports it isn't. The "stays filled when only a later constraint…" spec is the pin.

## Filter menu Apply button — hidden for auto-applying types

**Moot under `filterSurface: 'bar'`** — see "Operator-owned view" above, decision 1.

Which types auto-apply, why Apply there would lie, and why the method lists the auto types rather than the typed ones (safe polarity for future filter types): the `filterAppliesOnChange` doc comment. Clear stays either way — for a boolean it is the only path from checked/unchecked back to "no constraint".

**`[hideOnClear]="true"` is part of this, not a preference.** PrimeNG closes the filter menu only from `applyFilter()` (`_filter()` + `hide()`); `clearFilter()` hides only when `hideOnClear` is set, which defaults to false. So on a menu whose Apply is hidden, Clear was the one remaining button and it cleared without closing. Committing a *value* still leaves the menu open deliberately — a multiselect gets ticked several times per visit — so dismissal there is the popover's own (outside click, Esc).

**The e2e fixture mirrors this contract and must move with it.** `tests/e2e-fixtures/frontend/tests/e2e/page-objects/base-page.ts` → `applyBooleanFilter` presses no Apply and dismisses with Esc; it kept clicking the removed Apply for one CI run (33279746074) and timed out at 30s three times. Changing which types auto-apply changes that helper too.

## `Column.matchModes` — narrowing, and the two ways it must not half-apply

Consumer-facing behavior: `claude-plugins/docs/angular-customization/index.md` → the `matchModes` paragraph, and the `Column.matchModes` doc comment (declaration-time only; the default applies even without `showMatchModes`). Editing notes:

- **One resolution feeds both bindings.** `resolveMatchModes` computes the offered list and the default mode together and memoizes them per column (keyed on the declared array's identity), so `[matchModeOptions]` and `[matchMode]` can never disagree, and the narrowed array keeps a stable reference across change-detection passes.
- **PrimeNG turns a bad narrowing into a broken filter, not an error** — it resolves `matchModeOptions || <type defaults>`, and an empty array is truthy. So `[]` renders a dropdown with NO options while the default mode still seeds the constraint (and the generated backend answers an unsupported mode with a 400), and `null` silently restores PrimeNG's own list. `computeMatchModes` therefore drops unsupported codes, falls back to the full list when nothing survives, and `console.error`s in both cases rather than shipping an unusable menu.
- **Text has its own list now (`matchModeTextOptions`), and that is a fix, not decoration.** The library used to hand PrimeNG `null` for text, so a `showMatchModes` text column offered PrimeNG's six modes while `PaginatedResultGenerator.GetCaseForString` implements three — `notContains` / `endsWith` / `notEquals` each 400'd. It also made `matchModes` a silent no-op there, since the narrowing has nothing to narrow.
- **Persisted match modes are reconciled on init** (`reconcilePersistedMatchModes`) — the filter twin of `keepSortableMeta`. `ColumnFilter.ngOnInit` skips `initFieldFilterConstraint()` when the field already carries a restored constraint, so `[matchMode]` never reaches it; a mode stored before the column declared `matchModes` would keep filtering while its `<p-select>` rendered blank. It rewrites storage (before PrimeNG's `restoreState()` reads the same key) and touches only the offending constraint.
