# E2E Fixtures (Playwright)

How `tests/e2e-fixtures/` plugs into Spiderly's CI:

```
spiderly init        → creates a generic test app at $APP_FOLDER
add-new-entity X     → generates minimal entity scaffolding (lists, details, etc.)
setup.sh APP_NAME APP_FOLDER  → overlays our fixtures on top of the generated app
dotnet build / ng build      → built with the overlay applied
playwright test              → exercises the resulting app
```

`setup.sh` copies fixture files into the generated app *after* CLI generation, so any folder structure you add here must mirror the target path inside `$APP_FOLDER`. The current copy steps:

| Source | Target |
|---|---|
| `backend/entities/*.cs` | `$APP_FOLDER/Backend/$APP_NAME.Business/Entities/` |
| `backend/infrastructure/ApplicationDbContext.cs` | `$APP_FOLDER/Backend/$APP_NAME.Infrastructure/${APP_NAME}ApplicationDbContext.cs` |
| `frontend/tests/e2e/{helpers,specs,page-objects,fixtures}/` | `$APP_FOLDER/Frontend/e2e/` |
| `frontend/app/<entity>/<entity>-list.component.ts` | `$APP_FOLDER/Frontend/src/app/pages/<entity>/...` (overrides generated minimal list) |

## Generated lists ship with the Id column only

`spiderly add-new-entity` generates a list component with a single numeric Id column plus Details/Delete actions (see `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` → `GetSpiderlyAngularTableTsTemplate`). If a test needs to drive text/numeric/boolean filters, add a fixture component at `frontend/app/<entity>/<entity>-list.component.ts` with the columns you need and add a copy step in `setup.sh`. The Product fixture (`tests/e2e-fixtures/frontend/app/product/product-list.component.ts`) shows the full override pattern with text, numeric, and boolean columns.

## Designing fixture columns

For column-config behavior (when the match-mode dropdown renders, how labels resolve), see `Angular/projects/spiderly/src/lib/components/spiderly-data-table/CLAUDE.md`. Two points commonly bite e2e authors:

- Numeric/date columns need `showMatchModes: true` on the `Column<T>` for the match-mode `<p-select>` to render at all. The Price column in `frontend/app/product/product-list.component.ts` is the reference.
- Match-mode option labels are translocoService output (`'More than'`, `'Less than'`), not `MatchModeCodes` keys. Match Playwright selectors against the en.json value.

## PrimeNG v19 selector pitfalls

- **Filter Apply/Clear buttons have no identifying class.** PrimeNG's documented `pcFilterApplyButton` / `pcFilterClearButton` style classes are not applied to the rendered `<p-button>` elements. Match by accessible name: `overlay.getByRole('button', { name: 'Apply' })`.
- **Match-mode dropdown is `<p-select>`, not `<p-dropdown>`** — PrimeNG renamed Dropdown to Select in v19. Spiderly's `<spiderly-dropdown>` wraps `<p-select>` internally too.
- **Boolean filter is rendered as `<p-checkbox [binary]="true" [indeterminate]="value === null">`**, not as a `pTriStateCheckbox`. Initial filter state is `null` (shown as a horizontal dash); a click cycles `null → true → false → null`.
- **Filter overlays for the rightmost column get clipped against the viewport.** PrimeNG keeps repositioning the overlay frame-by-frame, so Playwright's stability check on inner elements fails (`47 × waiting for element to be visible, enabled and stable`). Pass `click({ force: true })` to bypass the stability gate. The Apply/Clear `getByRole` matchers do not need this — only the elements *inside* the overlay (e.g. `.p-checkbox-box`).

## Debugging a failing Playwright test in CI

When a selector times out, the failure log rarely shows enough context. Pull the trace artifact and look at the last screenshot — it reveals whether the element is missing, off-screen, occluded, or labeled differently than expected.

```bash
gh run download <run-id> --dir /tmp/ci-<run-id>
cd /tmp/ci-<run-id>/playwright-report/data
for z in *.zip; do unzip -o -d /tmp/traces/"${z%.zip}" "$z"; done
# Pick the trace folder for the failing test, then view the last screenshot:
for d in /tmp/traces/*/; do grep -lc "<spec-file>:<line>" "$d"*.trace 2>/dev/null | head -1; done
ls /tmp/traces/<picked>/resources/ | grep jpeg | sort | tail -1
```

## Test data conventions

Tests seed and clean their own data. Two patterns in use:

- **Per-suite seed in `beforeAll` / cleanup in `afterAll`** — for entities that other tests in the same describe might reuse.
- **In-test seed + describe-scoped cleanup array + `afterAll`** — when only one test needs the data (see `product-crud.spec.ts` table-state test). Use `Promise.all` for the seed and the cleanup; sequential 40× HTTP roundtrips noticeably slow CI.
