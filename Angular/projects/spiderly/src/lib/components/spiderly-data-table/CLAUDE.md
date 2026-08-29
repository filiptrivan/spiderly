# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

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
- **`behavior: 'instant'`, never `'auto'`.** `'auto'` defers to the scrolling element's computed CSS `scroll-behavior`, so a consumer app setting `html { scroll-behavior: smooth }` would animate every page flip from the paginator up. The spec asserts the options object verbatim for exactly this reason.
- **The target is the `#tableContainer` wrapper, not the host.** `:host` declares no `display`, so `<spiderly-data-table>` is `display: inline` and its `getBoundingClientRect()` is not a dependable box for the "is it above the viewport" test.
- **The guard is an explicit `rect.top < scrollMarginTop`, not `block: 'nearest'`.** `'nearest'` looks like it would skip a needless scroll for free, but its behaviour when the element overruns the viewport at both edges is the case we could not confirm against CSSOM-View (both the W3C and csswg copies truncate before the algorithm, checked 2026-08-29). The explicit check is two lines and testable. It is also what keeps a table rendered *below* other content — `UIControlTypeCodes.Table` inside a generated details page — from yanking that content away when everything was already visible.
- **The offset lives in CSS and is read back from CSS.** `scroll-margin-top: calc(var(--spiderly-topbar-height, 0px) + 1rem)` on `.spiderly-table-container` is the only declaration of it; the helper reads the element's computed value instead of taking a number, so no TypeScript here knows the shell's dimensions. The var itself belongs to the layout — `Angular/CLAUDE.md` → fixed-chrome offset.
- Shared with `spiderly-data-view`, which has the identical wrapper and had the identical bug, through `scrollElementIntoViewIfAboveViewport` (`services/helper-functions.ts`). That component has no spec of its own; the behaviour is pinned here.

## Filter-state persistence

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

## Column widths — `Column.minWidth`

`getColHeaderWidth(col)` takes the **column**, not the filter type — it needs `minWidth`. Why the defaults are generous and why the override is a floor rather than a width: the `Column.minWidth` doc comment.

## Active-filter header icon — the projected `filtericon` template

PrimeNG 19's menu-mode filter button renders identically whether the column is filtered or not (its class is static, so there is no CSS-only fix), and the worst case is a `stateStorage`-restored filter on reload: the table opens filtered with zero signal. The projected `pTemplate="filtericon"` replaces PrimeNG's `<FilterIcon>` with `pi-filter` / `pi-filter-fill` + primary colour — the fill change is the non-colour channel (WCAG 1.4.1); colour only amplifies. Two traps, both spec-pinned:

- **The icon reads APPLIED state (`appliedFilterKeys`), never `table.filters` — this is the trap, and it shipped once.** `ColumnFilter.onModelChange` writes every keystroke of a text/numeric filter straight into the meta and calls `_filter()` only for the auto-applying types, so `table.filters` also holds constraints the operator is still typing. Reading it filled the icon on the first character, claiming the grid was narrowed while it still showed everything (Filip, 2026-08-29, on the live admin). `snapshotAppliedFilters` records the set at each COMMIT point instead — `(onFilter)` (which `_filter()` emits for lazy and client tables alike), `lazyLoad` (also the `table.clear()` path, which re-queries **without** emitting `onFilter`), the caption's Clear filters, and once in `ngOnInit` off persisted state so a restored filter is marked on first paint. The "typed but not yet applied" spec is the pin; note a spec that sets `filters` without calling `_filter()` now proves nothing.
- **Two questions, two predicates — don't collapse them.** `isColumnFiltered(col)` answers "are the rows on screen narrowed by this column" (the icon). `columnHasConstraint(table, col)` answers "does this column carry a constraint at all, pending included" — which is the right question for `clearHiddenColumnConstraints`, since a typed-but-unapplied value must still be dropped when the column is hidden.
- **Truth is our `isActiveFilterMeta`, never the template's `let-hasFilter` context.** PrimeNG's getter reads only the *first* constraint of array meta, so a multi-constraint column whose first slot is blanked but whose second still holds a value is genuinely filtered while `hasFilter` reports it isn't. The "stays filled when only a later constraint…" spec is the pin.

## Filter menu Apply button — hidden for auto-applying types

Which types auto-apply, why Apply there would lie, and why the method lists the auto types rather than the typed ones (safe polarity for future filter types): the `filterAppliesOnChange` doc comment. Clear stays either way — for a boolean it is the only path from checked/unchecked back to "no constraint".

**`[hideOnClear]="true"` is part of this, not a preference.** PrimeNG closes the filter menu only from `applyFilter()` (`_filter()` + `hide()`); `clearFilter()` hides only when `hideOnClear` is set, which defaults to false. So on a menu whose Apply is hidden, Clear was the one remaining button and it cleared without closing. Committing a *value* still leaves the menu open deliberately — a multiselect gets ticked several times per visit — so dismissal there is the popover's own (outside click, Esc).

## `Column.matchModes` — narrowing, and the two ways it must not half-apply

Consumer-facing behavior: `claude-plugins/docs/angular-customization/index.md` → the `matchModes` paragraph, and the `Column.matchModes` doc comment (declaration-time only; the default applies even without `showMatchModes`). Editing notes:

- **One resolution feeds both bindings.** `resolveMatchModes` computes the offered list and the default mode together and memoizes them per column (keyed on the declared array's identity), so `[matchModeOptions]` and `[matchMode]` can never disagree, and the narrowed array keeps a stable reference across change-detection passes.
- **PrimeNG turns a bad narrowing into a broken filter, not an error** — it resolves `matchModeOptions || <type defaults>`, and an empty array is truthy. So `[]` renders a dropdown with NO options while the default mode still seeds the constraint (and the generated backend answers an unsupported mode with a 400), and `null` silently restores PrimeNG's own list. `computeMatchModes` therefore drops unsupported codes, falls back to the full list when nothing survives, and `console.error`s in both cases rather than shipping an unusable menu.
- **Text has its own list now (`matchModeTextOptions`), and that is a fix, not decoration.** The library used to hand PrimeNG `null` for text, so a `showMatchModes` text column offered PrimeNG's six modes while `PaginatedResultGenerator.GetCaseForString` implements three — `notContains` / `endsWith` / `notEquals` each 400'd. It also made `matchModes` a silent no-op there, since the narrowing has nothing to narrow.
- **Persisted match modes are reconciled on init** (`reconcilePersistedMatchModes`) — the filter twin of `keepSortableMeta`. `ColumnFilter.ngOnInit` skips `initFieldFilterConstraint()` when the field already carries a restored constraint, so `[matchMode]` never reaches it; a mode stored before the column declared `matchModes` would keep filtering while its `<p-select>` rendered blank. It rewrites storage (before PrimeNG's `restoreState()` reads the same key) and touches only the offending constraint.
