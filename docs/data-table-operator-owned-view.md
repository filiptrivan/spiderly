# Data table: the operator-owned view — Design

> Status: **SHIPPED IN FULL, legacy path DELETED (2026-09-05).** Every decision below is built, all
> 27 PACMS tables carry a store, and the legacy header-filter path (`p-columnFilter`, the projected
> filtericon, Apply/hideOnClear, match-mode narrowing, the "hidden contributes nothing" trio,
> `Column.filterField`/`filterPlaceholder`/`matchModes`/per-column `showAddButton`) is gone — a
> table handed no store now has NO filter surface at all. This note stays as the decision record;
> the deletion-wave rulings live in the bullet list under "What has shipped" below, and interior
> decision prose keeps its original tense as history — the rulings win where they disagree.
>
> Decided 2026-09-02 (Filip, grilled). This record lived in the component's own `CLAUDE.md` until
> 2026-10-28, when it moved here; `spiderly-data-table/CLAUDE.md` → "Operator-owned view" points
> here, and the component's comments cite the decisions below by number ("decision 8",
> "CLAUDE.md -> decision 8").

## The premise

**The premise that broke.** Every presentational knob on this grid is set by whoever writes
`Column[]`: which columns show, their width, their order, whether a value is clamped — and, because
`clearHiddenColumnConstraints` drops a hidden column's constraints, which filters an operator may
even use. The operator gets no say. Five complaints landed in one sitting against the PACMS orders
grid, and they are one feature rather than five: **presentation belongs to the operator, the data
contract stays with the developer.**

## Decisions

Decisions, in the order they were resolved. Do not re-derive them. **Decisions 1 and 2 were
rewritten later the same day**, after Filip proposed the filter engine below; the earlier form
kept filters as a property of `Column` behind a `filterSurface` flag, and is in the git history of
`spiderly-data-table/CLAUDE.md` only, where this record lived until 2026-10-28.

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
  for exactly one of the 27 (PACMS's order list, its `buildColumns`).

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
PrimeNG 19.1.3's primeng-table.mjs: `saveColumnWidths` stores `widths.join(',')` and `restoreColumnWidths`
replays them through `th:nth-child(n)` — index-keyed, so one chooser toggle slides every stored width
onto the wrong column, and so does adding a column in a later release. `restoreColumnOrder` is
commented out of `restoreState` outright. `onColumnDrop` reorders `this.columns`, an input this
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

## What has shipped

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
  it unbuilt until something asked, and `Filter…` asked.
  Decision 9's `title`-on-overflow directive shipped too (`SpiderlyOverflowTitleDirective`).
  Header drag (decision 6), the frozen left edge (decision 8), `spiderlyFilterTemplate` and
  views (decision 10) all shipped too — every decision in this record is now built. Decision
  10's SORT half arrived last (2026-09-05, for PACMS's "Poslate, oldest first" order view):
  `TableView.sort` declares a view's ordering, the tri-state un-sort lands on it
  instead of the table default (`defaultMultiSortMeta` is view-aware now), and the operator's
  own sort persists per view under `…:<viewId>:sort` — written only by explicit gestures, so a
  changed declared sort flows through to operators who never sorted, same rule as column
  visibility.
  **Its "no preference inherits" half was SUPERSEDED one day in (2026-09-06, grilled from the
  first live complaint):** as shipped, a view declaring no sort kept whatever ordering was on
  the grid, and PrimeNG's table-global persisted sort backed that up at init — so "Poslate"'s
  `statusChangedAt` rode onto "Sve" and survived every F5, with no way out (sort chip
  read-only, no clear button without filter chips, the sorted column hidden). Now `applyViewSort`
  resolves override ?? declared ?? table default ?? UNSORTED — a view is a COMPLETE state,
  sort included, which is Polaris IndexFilters' model (sort is part of a saved view's
  definition: `onSortKeyChange`/`onSortDirectionChange` exist exactly for that) — and on a
  viewed table the table-global blob is a NON-SOURCE for sort at init (view-less tables keep
  it as their one sort memory). Two riders landed with it: **Clear filters
  clears only the filters** (Polaris wires onClearAll to the Filters; sort is its own channel
  — `clear()` no longer calls `table.clear()`/`clearState`, which also removed its
  unsequenced double fetch), and **the sort chip became the always-present sort PICKER**
  (Polaris SortButton): a popover of every sortable column, hidden ones included — otherwise
  unreachable — ascending on pick, the active ascending option flips, a reset row only while
  off the resolved default; no "clear sort" ✕, since the default is just one of the reachable
  orderings. The bar decides the direction so the owner applies what the menu showed;
  `sortFromBar`/`resetSortFromBar` reuse the header-menu contract.
  **The declared-COLUMNS half shipped 2026-10-11 (spiderly#431).** The trigger this record
  named arrived: PACMS's order views needed a "Zašto" column on eight work lists, in the place
  of a column the catch-all views keep (the first signal was "Poslate" shipping `deliveredAt`
  `visible: false` and telling operators to add it by hand). `TableView.columns` is a
  field → visible map, resolved per field as operator override ?? the view's `columns` ??
  `Column.visible` (`isDeclaredVisible` is the middle rung), so the override stays a delta
  against whatever the view declares — `toggleColumn` compares against it, and Reset lands on
  it. Rejected: an exhaustive `columns: string[]` per view (the issue's first suggestion). It
  restates every shared column in every view that changes one, and a column added to `cols`
  later would be silently missing from each such view until someone edits its list; the map
  names only the deviation. Declared per-view widths and order stay unbuilt — nobody has asked.
- **The legacy header-filter path is DELETED (2026-09-05), with four rulings made at the wave:**
  - **`spiderly-data-view` is EXCLUDED, deliberately.** It has its own parallel filter config
    (`DataViewFilter` — its own `filterField`/`showMatchModes`) and no bar equivalent, so
    deleting its header filters would leave a fresh `spiderly init` app's data view with no
    filtering at all. It keeps `p-columnFilter` until it either grows a bar or is migrated onto
    the store engine — a decision for its own rework, not a leftover to "clean up" on sight.
    (Its operator lists are no longer hand-kept, though — they derive from
    `operatorOptionsForKind` since the review sweep.)
  - **`operatorOptionsForKind` was deleted with the path, then CAME BACK in the review sweep
    (2026-09-05):** the "no kind-only callers left" claim missed `spiderly-data-view`'s
    hand-kept copies of the per-kind lists (its doc comment).
  - **`Column.showMatchModes` AND `Column.filterField` survive as dead, `@deprecated` props —
    a version-skew bridge, not an oversight** (the window and when to delete them: their
    `@deprecated` docs). (`filterField` was dropped outright at
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
  agreed to wait for a signal rather than build it. Do not build it on a hunch: if it lands, it needs
  saving a named snapshot, a list mixing built-in and personal, deletion, and a name-collision
  rule — and if views must be SHARED between operators it stops being localStorage and becomes a
  table, a CRUD surface and permissions.
- **What the orders migration added (2026-09-05),** each spec-pinned; pointers, not copies:
  - Per-filter operator narrowing (`FilterConfig.operators`, whose doc carries its semantics) is
    resolved ONCE per store (a per-store map, not the
    WeakMap this first shipped as — that memo was keyed on a definition object consumers
    mutate); the one kind-only caller left (the data view's lists) uses `operatorOptionsForKind`.
  - Handles read `setOptions`' late-arriving choices via live getters, so an editor already open
    when the lookup answers fills instead of staying empty (the seam itself, and `setAndCommit`:
    their doc comments).
  - `chipValue` speaks a pick-list's OPTIONS' labels, read live. Follow-up
    if a second chip-drawing surface ever appears: make options a signal and let `applied()`
    carry a `valueLabel`, so the whole chip sentence has one home.
  - **Declined the same day: extracting the layout state (`columnWrap`/`columnOrder`/
    `columnWidths`) into a `ColumnLayout` class.** A pure moving-of-fields with no behavior
    change, in the file the migration was actively stabilising; nothing currently duplicates or
    misuses that state, so the refactor bought a diff and no property. Revisit only if a second
    component needs the same layout model.

## Rollout

**Rollout, three waves, each a spiderly release plus a PACMS upgrade.** (1) The filter engine, the
slot directive and the chip bar — decisions 1, 2 and 2b, with 9's `title` directive as a rider, and
the 27 PACMS tables migrated one at a time across it. (2) Header menu, chooser drag list, own reorder directive,
field-keyed width/order/wrap persistence, frozen edge — decisions 3 to 9. (3) Views — decision 10.
**Wave 3 is not optional.** Without it, waves 1 and 2 are a pile of knobs an operator re-sets by hand
every morning, and the smaller honest plan (the `title` directive, a two-item header menu, and a bare
"N filters on hidden columns · Show" strip instead of the bar) would have been the better trade.
Filip took the full plan on that condition.
