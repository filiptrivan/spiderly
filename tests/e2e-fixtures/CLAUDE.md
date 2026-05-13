# E2E Fixtures (Playwright)

This file covers the **framework-internal** overlay mechanics that turn `tests/e2e-fixtures/` into Spiderly's own CI suite. For test-authoring patterns that also apply to consumer apps (login helper, PrimeNG v19 selectors, trace debugging, data seeding), see the `e2e-testing` consumer skill at `claude-plugins/skills/e2e-testing/SKILL.md`.

## How the fixture overlay plugs into CI

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

## Overriding the generated list inside the fixture suite

`spiderly add-new-entity` generates a list component with a single numeric Id column plus Details/Delete actions (see `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` → `GetSpiderlyAngularTableTsTemplate`). To drive text / numeric / boolean filter tests, add a fixture component at `frontend/app/<entity>/<entity>-list.component.ts` with the columns you need, then add a copy step in `setup.sh`. The Product fixture (`tests/e2e-fixtures/frontend/app/product/product-list.component.ts`) is the reference — text, numeric, and boolean columns wired with the correct match-mode flags.

The *behavior* (generated lists are Id-only) is also documented in the `e2e-testing` consumer skill, but the override-via-fixture-overlay pattern is internal to this suite.
