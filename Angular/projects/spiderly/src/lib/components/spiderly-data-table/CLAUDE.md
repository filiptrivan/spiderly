# SpiderlyDataTableComponent

Wraps PrimeNG v19 `<p-table>` and exposes Spiderly's column-based filter / sort / pagination model. Notes for anyone editing the component or designing `Column<T>[]` configurations in consumer code.

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

## Filter-state persistence

`@Input() stateKey?: string` plus `@Input() stateStorage: 'session' | 'local' = 'session'` light up PrimeNG's stateful-table behavior. When `hasLazyLoad` is true, `ngOnInit` derives `resolvedStateKey` from `router.url` (plus `additionalFilterIdLong` to disambiguate parent-child views). Consumers don't normally pass `stateKey` — leave it auto-derived. The `clear(table)` method also calls `table.clearState()` so the "Clear all filters" caption button wipes the persisted state instead of just resetting the in-memory table.
