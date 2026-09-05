# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

## Operator-owned view — the rework decided 2026-09-02 (Filip, grilled)

**Status: SHIPPED IN FULL, legacy path DELETED (2026-09-05).** Every decision below is built, all
27 PACMS tables carry a store, and the legacy header-filter path (`p-columnFilter`, the projected
filtericon, Apply/hideOnClear, match-mode narrowing, the "hidden contributes nothing" trio,
`Column.filterField`/`filterPlaceholder`/`matchModes`/per-column `showAddButton`) is gone — a
table handed no store now has NO filter surface at all. This block stays as the decision record;
the deletion-wave rulings live in the bullet list under "What has shipped" below, and interior
decision prose keeps its original tense as history — the rulings win where they disagree.

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
- **`Column.filterId` is NOT built yet, deliberately (2026-09-04). [Superseded: it shipped with
  the header menu's `Filter…` — see "What has shipped".]** With the bar owning every
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
- `filterField` disappears with this. It existed only as the multiautocomplete patch
  (`filterKey(col)` = `filterField ?? field`), i.e. as the coupling already cracking — both were
  deleted with the wave (the type keeps a dead `@deprecated filterField` as the same
  version-skew bridge as `showMatchModes`; see the rulings).
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
exactly the right column in every table, so this needs no new API. Costs to plan for: a sticky BODY
cell needs an opaque background that tracks row hover and striping — the header th instead keeps
PrimeNG's own header palette and per-cell sortable hover (painting the row palette there tinted the
pinned th whenever the pointer crossed any header cell; the SCSS carries the telling) — and the
second frozen column's offset needs the first one's measured px width.

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
- **The legacy header-filter path is DELETED (2026-09-05), with four rulings made at the wave:**
  - **`spiderly-data-view` is EXCLUDED, deliberately.** It has its own parallel filter config
    (`DataViewFilter` — its own `filterField`/`showMatchModes`) and no bar equivalent, so
    deleting its header filters would leave a fresh `spiderly init` app's data view with no
    filtering at all. It keeps `p-columnFilter` until it either grows a bar or is migrated onto
    the store engine — a decision for its own rework, not a leftover to "clean up" on sight.
    (Its operator lists are no longer hand-kept, though — they derive from
    `operatorOptionsForKind` since the review sweep.)
  - **`operatorOptionsForKind` was deleted with the path, then CAME BACK in the review sweep
    (2026-09-05):** the "no kind-only callers left" claim missed that `spiderly-data-view`
    hand-keeps copies of the same per-kind lists — the exact drift `allowed-operators.ts`
    exists to end. The data view now derives its lists through it; per-filter narrowing still
    lives in the store.
  - **`Column.showMatchModes` AND `Column.filterField` survive as dead, `@deprecated` props —
    a version-skew bridge, not an oversight.** An older Spiderly.SourceGenerators (published
    NuGet) still emits `showMatchModes: true` for scalar and `filterField: 'xxxId'` for
    dropdown details-table cols (`base-details.generated.ts`), and a consumer in mixed dev
    state (npm-linked new library + NuGet old generators — pa-cms's normal dev mode) must keep
    compiling. Nothing reads either. Delete both once consumers regenerate with a generator
    from this release train or later (spiderly#407). (`filterField` was dropped outright at
    first; the review caught that the bridge was asymmetric — one dropdown `[UITableColumn]`
    away from breaking the exact window it exists for.)
  - **Generated details-page M2M tables (`[SimpleManyToManyTableLazyLoad]` /
    `[ComplexManyToManyReadonlyTable]`) ship with NO filter surface** until
    `NgDetailsDataGenerator` scaffolds a store for them — the generator stopped emitting the
    header-filter props, and scaffolding a store there (options plumbing for dropdown columns
    included) is its own upstream piece of work — spiderly#407, which also carries the
    `Column.showMatchModes` bridge-prop cleanup. The only PACMS instance is the Cart items
    readonly grid (2 columns).
- **The consumer migration is COMPLETE — all 27 PACMS tables carry a store (2026-09-05).**
  Three earned views: `tag-list` (three), `order-list` (the table the rework was argued from:
  17 filters, six views — a catalog-driven "Za pakovanje" hidden mid-deploy while no row
  claims it, a transient "Danas primljene" — and the search box as a placement of the store's
  `mixedSearch` handle), and `product-list` (Istaknuto / Neobjavljeno, both flags measured
  NULL-free on prod before trusting `Equals` on a `bool?` column). The other 24 were judged
  filter-only; two of those judgements are recorded in their factories because they will be
  challenged: `product-review-list` (the moderation view is refused by DATA — the pending
  queue lives in `IsApproved IS NULL`, a question no boolean filter can ask; unblock is
  backend normalization, then the view) and `user-list` (every question there is a lookup,
  not a recurring cut). `integration-matching-products` builds a store per MODE and nulls its
  view model on every integration change — the recreation pattern any consumer swapping
  stores needs, because the table reads `[filters]` and derives its state key only in its own
  ngOnInit. With nothing left passing the old shape, wave 1's tail — deleting the legacy
  header-filter path — is unblocked.
- **Custom views were asked for and PARKED (Filip, 2026-09-05).** Views ship declared in consumer
  code; an operator cannot save their own. Filip asked for that after using the tags grid, then
  agreed to wait for a signal rather than build it — Plaky "Waiting" 7387519 carries the full
  reasoning and what would have to happen. Do not build it on a hunch: if it lands, it needs
  saving a named snapshot, a list mixing built-in and personal, deletion, and a name-collision
  rule — and if views must be SHARED between operators it stops being localStorage and becomes a
  table, a CRUD surface and permissions.
- **Two rules a later table must not re-derive:** a view CLEARS before it applies, so two never
  compose; and both layout keys carry the active view, so a table with no views reads exactly the
  keys it already wrote — which is what kept each not-yet-migrated grid working untouched while
  the 27 landed one at a time.
- **What the orders migration added (2026-09-05),** each spec-pinned; pointers, not copies:
  - `commit()` bails on an EQUAL constraint, not an equal draft object (`valueEquals`) — a paste
    over itself or a double Apply spends no request; the page-side `lastCommittedTerm` guard this
    retires was PACMS's hand-rolled copy.
  - Per-filter operator narrowing (`FilterConfig.operators`, on every factory and TYPED per
    kind, so a wrong operator is a build error at the declaration): offer-only, first entry is
    the default, a dynamically built bad entry still dropped loudly with full-list fallback —
    `Column.matchModes` semantics on the store side. The wire contract stays ALLOWED_OPERATORS;
    a restored old operator keeps filtering. Resolved ONCE per store (a per-store map, not the
    WeakMap this first shipped as — that memo was keyed on a definition object consumers
    mutate); the one kind-only caller left (the data view's lists) uses `operatorOptionsForKind`.
  - `setOptions(id, options)` is the ONE seam for late-arriving choices — it re-resolves the
    offered operators (options' presence flips a filter to `In`) and handles read it via live
    getters, so an editor already open when the lookup answers fills instead of staying empty.
    Never write `definitions[id].options` by hand. `setAndCommit` is the one-breath write every
    programmatic caller (view `apply`, the sheet flow) uses — a bare `set` with a forgotten
    commit fails silently.
  - A pick-list chip speaks its OPTIONS' labels, read live off the definition so async-filled
    options upgrade a restored chip from ids to labels when they land (`chipValue`). Follow-up
    if a second chip-drawing surface ever appears: make options a signal and let `applied()`
    carry a `valueLabel`, so the whole chip sentence has one home.
  - **The "hidden contributes nothing" apparatus is GONE on store tables** (while both paths
    coexisted it was gated off there) — decision 2b's other half: hiding a column keeps its
    filter AND sort, `defaultMultiSortMeta` applies the default even on a hidden column, and
    stale header-filter meta in an old persisted blob never reaches a request (`lazyLoad`
    overwrites `event.filters` with the store payload, or `{}` on a storeless table —
    unconditional, so the guarantee holds by construction). The bar and sort chip are the
    visible surface for all of it. **STORELESS tables keep the SORT half** (the review sweep,
    2026-09-05): they have no chip to name a hidden column's sort, so hide and layout-reset
    drop it and a hidden `defaultSortField` does not apply — `dropHiddenColumnSort`, with its
    own spec suite. The filter half needs no storeless heir, per the unconditional overwrite
    above.
  - `TableView.transient` — a view whose `apply` is a function of NOW ("Danas primljene")
    re-applies on every select instead of restoring; stored-wins would put yesterday's date
    under a tab claiming "today". Layout still persists per view.
  - **The operator table has ONE home now:** `filters/allowed-operators.ts`, a leaf module the
    store's runtime checks, `FilterRule`'s compile-time unions and the data view's kind lists
    all derive from (the three hand-kept copies this file's git history warned about). Tuple
    order is display order everywhere.
  - **Declined the same day: extracting the layout state (`columnWrap`/`columnOrder`/
    `columnWidths`) into a `ColumnLayout` class.** A pure moving-of-fields with no behavior
    change, in the file the migration was actively stabilising; nothing currently duplicates or
    misuses that state, so the refactor bought a diff and no property. Revisit only if a second
    component needs the same layout model.

**Three more traps, all found by a spec and none visible by eye:**

- **`ResizeObserver`'s first callback lands an animation frame after paint.** The frozen column
  sat at `left: 0`, on top of the checkbox column it starts after, for that frame — and its own
  spec had been passing on timing luck until adding suites reordered the run. Measure once on a
  microtask as well; writing it inline in `ngAfterViewInit` is an NG0100. The microtask measure
  itself races the first row render (rows land on a macrotask), so where the rendered rows
  re-split the shares the correction arrives at frame timing `whenStable` never waits for —
  Linux headless Chrome re-split by 2px while macOS did not (CI run 33943130146, 2026-09-05),
  a green local suite proving nothing. A spec asserting the offset must settle animation frames
  first: `settleFrozenOffset` in the spec.
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

## Operator labels are runtime translations

The bar's operator picker and chip phrases resolve their labels through `translocoService.translate(...)` from `OPERATOR_WORDS` (`filters/filter-store.ts`), so the user-visible text is the **value** in `assets/i18n/<locale>.json`, not the key. The picker keys in English: `Equals`, `LessThan` → `Less than`, `MoreThan` → `More than`, `OnDate`, `DatesBefore`, `DatesAfter`, `StartsWith`, `Contains`. When matching options programmatically (e2e tests, conditional logic), match against the rendered label, not the key; renaming a key without updating en.json (or vice versa) silently breaks consumers that match by label, and the keys must stay seeded in the init template's en block (`NetAndAngularFilesGenerator.cs`).

## Column chooser — `Column.visible` / `Column.lockVisible`

Consumer-facing behavior is documented in `claude-plugins/docs/angular-customization/index.md` → "Column chooser". Editing notes:

- **Hiding a column touches nothing but visibility** — its filter and sort survive, named by the chip bar and sort chip (decision 2b). The old "hidden contributes nothing" invariant and its three enforcement points died with the legacy header-filter path.
- Template loops iterate `visibleCols` (recomputed via `refreshVisibleCols()`), never `cols`. Actions columns (no `field`) always render and never appear in `chooserCols`.
- Overrides live in `columnVisibilityOverrides` — **only fields the user explicitly toggled off their declared default** (toggling back to default deletes the entry). Persisted to `` `${resolvedStateKey}:columns` `` in **localStorage always** (column layout is a durable preference), unlike the filter state which follows `stateStorage`. `lockVisible` beats a stale persisted override.
- Filter ids and sort meta are both keyed by `field` now (a column with a differently-named filter links it via `filterId`; `filterField` is gone with the header filters).
- The chooser only renders for lazy tables (`hasLazyLoad`); client-side form-array tables keep their declared columns.
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

**Known follow-up, not fixed here (2026-08-30):** the `lazyLoad` subscription is unmanaged — no `switchMap`, no `takeUntil(destroy$)` — so pending is a boolean rather than a property of an identified in-flight request. Two overlapping refetches (plausible with `[filterDelay]="500"` plus a page click) let the first response lower the flag while the second is still outstanding, and they can land out of order. Pre-existing, but this work is what made `loading` load-bearing for what the reader sees. A `switchMap` over a subject fixes ordering, cancellation and the leak together. **The bar's Clear made this concrete on every store table (found by review, 2026-09-05):** one Clear gesture fires TWO identical requests — `clear(table)` runs the store's `clear()` (whose requery effect issues one fetch) AND `table.clear()` (PrimeNG synchronously emits its own lazy load) — with nothing sequencing them, so they can land out of order. Fix it with (or as part of) the `switchMap` work rather than a one-off guard.

Spec note: any test that drives a refetch must do it inside `fixture.ngZone.run(...)`, or the `delay(0)` is scheduled where `whenStable` will not wait and the assertion measures a still-pending table. That trap is documented once, on `swapRowNameTo`; three older specs call `table._filter()` unwrapped and are exposed to it.

## State persistence

`@Input() stateKey?: string` plus `@Input() stateStorage: 'session' | 'local' = 'session'` light up PrimeNG's stateful-table behavior (sort, pagination) under `resolvedStateKey`; the store's applied filters persist beside it under `` `${resolvedStateKey}${viewScope}:filters` `` in the `stateStorage` storage. **The view segment is on the FILTER key too, not just the layout keys** (decision 10 — a view IS a saved question): on a table with views the segment is always present — `activeViewId` restores from `` `…:view` `` (below) and seeds from the first view only when nothing is stored — so on PACMS `order-list` the key is `…:<active view>:filters` and a bare `…:filters` never exists. A table with no views has no segment — which is what keeps its keys stable. When `hasLazyLoad` is true, `ngOnInit` derives `resolvedStateKey` from `router.url` (plus `additionalFilterIdLong` to disambiguate parent-child views). Consumers don't normally pass `stateKey` — leave it auto-derived. The `clear(table)` method (the bar's Clear filters) also calls `table.clearState()` so the persisted state is wiped, not just the in-memory table.

**The active view survives a reload (2026-09-05).** The id persists under `` `${resolvedStateKey}:view` `` — deliberately NOT view-scoped (it is what `viewScope` derives from), and in the `stateStorage` storage like the filters rather than always-local like the layout, because the id and the filters it scopes are one question: a durable id over session-scoped filters would restore a tab with nothing under it. The gap this closes: every persisted key carried the view segment while the segment's source lived only in memory, so an F5 seeded `views[0]` back and restored `…:all:filters` — tab on "Sve", bar empty, the operator's filters intact under the abandoned segment (Filip, on /porudzbine; the rework's decision record never addressed reload). Rules, each spec-pinned in the "active view survives a reload" suite:

- Only an explicit `selectView` writes the key (absence = first view); `ngOnInit` restores it BEFORE the layout/columns/filters restores, which all read `viewScope`.
- A stored id naming no current view keeps the seed AND the key — "vanished by deploy" and "not yet arrived" are indistinguishable at init, and order-list's catalog-licensed "Za pakovanje" is the latter on every load. The id parks in `pendingStoredViewId` and `ngOnChanges` adopts the view through `selectView` when it arrives (same stored-wins/transient/layout/requery semantics as a click), unless the operator picked a tab since — their click always wins. Accepted cost: a flash of the seeded view and a second request when the late view lands.
- A restored TRANSIENT view keeps its tab but re-runs `apply` instead of `restoreAppliedFilters` — `persistAppliedFilters` has no transient guard, so storage holds yesterday's answer. A non-transient view whose stored filters are absent stays empty: the operator cleared it, and F5 returns the cleared state; re-asking the view's question is click semantics.

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

One rider from the rework is still open: `DEFAULT_COLUMN_WIDTH_REM` still carries the numbers
sized for the header-filter era (decision 2b said to re-derive them from VALUES once the header
carried no input); nobody has done that measurement yet, and the per-column drag/fit makes it
low-stakes.

`getColWidth(col)` takes the **column**, not the filter type — it needs `Column.width`. Why the defaults are generous, and why they are a width rather than a minimum: the `Column.width` doc comment and `tableStyle`.

Editing notes:

- **`width: 0rem` is not "shrink to content" any more.** It meant that under auto layout; fixed layout reads it literally and the column vanishes. Two sites had it and both now carry a real width — the selection `<th>` in the template, and the fallthrough in `getColWidth` (actions columns, sized from `actions.length`, against a gap set beside them in the template).
- **`DEFAULT_COLUMN_WIDTH_REM` is exhaustive on purpose.** It replaced a `switch` whose `default:` silently absorbed `blob`, sizing a 45px thumbnail's column at 2rem. A new `filterType` now fails the build there instead of inheriting the actions reservation.
- **The table needs no `min-width` of its own** — don't add a computed floor to `tableStyle`. The browser already widens a table past its container to the sum of its declared column widths, so `overflow-auto` still scrolls; the "too wide for its container scrolls" spec holds the measurement.
- **The library styles `#defaultCell` directly; a projected template is reached through custom properties, never selectors.** `.cell-text` is authored in this template, so a plain `:host` rule matches it. A consumer's `spiderlyCellTemplate` renders markup carrying the CONSUMER's `_ngcontent` and is unreachable from here by selector — don't reach for `::ng-deep`. What DOES cross that boundary is inheritance: `td.cell-wrap` publishes the `--spiderly-cell-*` properties (see the SCSS), the default cell's own clamp reads them with the clamp as fallback, and a consumer clamp written the same way follows the operator's wrap toggle for free (consumer-facing recipe: `claude-plugins/docs/angular-customization/index.md` → column widths). Before this contract the toggle was a silent no-op on any templated column — every visible column of the PACMS orders grid — while the menu showed a checkmark. A consumer clamp that hard-codes `white-space: nowrap` still no-ops; that is the residue the contract accepts, not a bug in the toggle.

## The e2e fixture drives the bar, and must move with it

`tests/e2e-fixtures/frontend/tests/e2e/page-objects/base-page.ts` holds the filter helpers (`applyTextFilter` / `applyNumericFilter` / `applyBooleanFilter` / `clearTableFilters`), addressed by the bar's `data-testid`s and committing every kind through `filter-editor-apply`; each commit awaits its own paginated-list response because the table does not sequence concurrent lazy loads. Changing the bar's DOM, the editor's commit flow or the persistence keys changes those helpers and `specs/product-crud.spec.ts`'s storage asserts too — the stale-helper failure mode is a 30s Playwright timeout on an element that no longer exists (it happened to the old header-filter helper on CI run 33279746074).
