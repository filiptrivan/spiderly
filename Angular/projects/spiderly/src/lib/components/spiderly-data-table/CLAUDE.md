# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

## Custom toolbar actions — `<ng-template spiderlyDataTableActions>`

Consumers add their own toolbar buttons/markup by projecting an `<ng-template spiderlyDataTableActions>` (the `SpiderlyDataTableActionsDirective` marker). The component picks it up via `@ContentChild(SpiderlyDataTableActionsDirective, { read: TemplateRef })` and renders it with `*ngTemplateOutlet` at the **start** of the caption's right-side action row — `*ngIf`-guarded so an un-projected slot renders nothing (no stray flex gap).

- **Rendered before the built-ins on purpose.** Delete Selected is conditional, so trailing custom buttons would shift when selection toggles. Leading keeps them positionally stable. If you reorder the caption, keep the outlet first.
- **No context is passed** to the template (it binds to the consumer's component). This is deliberate: lazy-load selection has no clean flat-id representation (`newlySelectedItems`/`unselectedItems` under select-all). If a future need appears, add `ngTemplateOutletContext` keys — that's non-breaking, existing templates ignore unknown `let-` vars.
- The contract is covered by `spiderly-data-table.component.spec.ts` (the library's TestBed suite; runs via the `Unit Tests (Angular)` CI job, `karma.conf.js` → `ChromeHeadlessNoSandbox`).

## Shift-click range selection — anchor model

Selection checkboxes support Gmail-style ranges: shift+click applies the clicked checkbox's new state to every row between it and the anchor (`rangeAnchorId`). Consumer-facing behavior: `claude-plugins/docs/angular-customization/index.md` → "Row selection". Editing notes:

- **The shift state lives on the mousedown, not on the checkbox event.** `p-checkbox` emits the DOM `change` event as `onChange.originalEvent`, and `change` carries no `shiftKey` — so `onSelectionCellMouseDown` captures it into `pendingShiftRange`, which the same click's `selectRow` consumes-and-clears. A keyboard toggle (Space) never sets it. Don't "simplify" back to reading the event.
- **Range positions resolve by id over `items` at click time** (`findIndex`), never via `rowData.index` — only `loadFormArrayItems` stamps `.index`; lazy rows never carry one.
- **Every change goes through `toggleRow`** (the former `selectRow` body): that is what keeps the lazy delta model (`newlySelectedItems`/`unselectedItems`) and per-row event emissions identical to manual clicks. Rows already in the target state are skipped — the skip is what prevents duplicate ids in the delta arrays.
- **The anchor resets wherever the rendered rows change**: `lazyLoad`, `loadFormArrayItems`, and `onPageChange` (client-side page flips re-render different rows without touching `items`). A new path that swaps rows must reset it too.
- The selection `<td>` stops click propagation (a selection gesture must never double as `navigateOnRowClick` navigation) and suppresses the browser text selection at shift-mousedown, with `user-select: none` as belt.

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
- Filter state is keyed by `filterField ?? field`; sort meta by `field`. Both `clearHiddenColumnConstraints` and the reconciliation check accordingly — keep that symmetry when touching either.
- v1 scope: the chooser only renders for lazy tables (`hasLazyLoad`); client-side form-array tables keep their declared columns. Reordering/width persistence deliberately out of scope.
- Spec gotcha: the chooser checkboxes' `[ngModel]` writes resolve in a microtask — tests must `await fixture.whenStable()` after opening/toggling (see `openChooser` in the spec). Once open, PrimeNG appends the popover to `document.body`, so specs query the `Popover` instance's `container`, never the document (stale popovers from earlier fixtures linger there).
- That teleport also means chooser styles are declared at SCSS **top level**, never under `:host` (see `Angular/CLAUDE.md` → overlay styling); every chooser rule gets a row in the spec's `stylePins` table.

## Rows-per-page — `rowsPerPageOptions`

Default `[10, 25, 50, 100]`; `ngOnInit` merges the effective initial `rows` into the list when missing — a `rows` value outside the options leaves PrimeNG's paginator dropdown blank, so keep the merge when touching init order. The user's pick persists for free through PrimeNG table state (`saveState`/`restoreState` carry `rows`) under `resolvedStateKey` — do NOT add custom persistence; a durable (localStorage) page size was considered and deliberately deferred (2026-08-17) until usage asks for it. The 100 ceiling is a UI courtesy only: the backend `.Take(filterDTO.Rows)`s whatever it is sent, uncapped.

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
