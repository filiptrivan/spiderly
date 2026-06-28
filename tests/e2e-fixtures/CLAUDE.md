# E2E Fixtures (Playwright)

This file covers the **framework-internal** overlay mechanics that turn `tests/e2e-fixtures/` into Spiderly's own CI suite. For test-authoring patterns that also apply to consumer apps (login helper, PrimeNG v19 selectors, trace debugging, data seeding), see the `e2e-testing` consumer doc at `claude-plugins/docs/e2e-testing/index.md`.

## Why a codegen change is not "done" until the e2e is green

Generator unit tests (`Spiderly.SourceGenerators.Tests`) snapshot the **generated text** — they never **compile or run** it, and they run in a **single compilation** where there are no cross-project *referenced* entities. So a whole class of bug is invisible to them and **only** this e2e (which builds, migrates, boots, and drives a real `spiderly init` app on real Postgres) catches it. Adding native one-to-one support surfaced five such bugs, none of which broke a single snapshot test:

- Generated C# that references a member the DTO doesn't have (`dto.{Nav}Id` on a side that owns no FK) → CS1061.
- A `long?`/`long` mismatch in a generated query (`List<long>.Contains(x.{nullableFk})`) → CS1503.
- A **multi-project** inconsistency: a `.WebAPI` generator iterates entities as *referenced* classes, so a flag set only on *current-project* classes left two generators disagreeing → CS1061. The single-compilation unit harness **cannot** reproduce this.
- A runtime `NullReferenceException` in the EF model-config pass that only fires when the **full** `OnModelCreating` runs (the isolated model test missed it).
- A new entity with no seeded permissions → 403 at runtime.

Rule of thumb: **any change to a source generator or the EF model-config extensions must add/extend an e2e fixture entity and go green in CI before it ships.** Snapshot tests are necessary but never sufficient. When the model test exercises a relationship, run the *same* `Configure*Relationships` passes in the *same order* as the real `ApplicationDbContext.OnModelCreating`, or it will miss inter-pass NREs.

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
| `backend/infrastructure/ApplicationDbContext.SeedData.cs` | `$APP_FOLDER/Backend/$APP_NAME.Infrastructure/${APP_NAME}ApplicationDbContext.SeedData.cs` |
| `frontend/tests/e2e/{helpers,specs,page-objects,fixtures}/` | `$APP_FOLDER/Frontend/e2e/` |
| `frontend/app/<entity>/<entity>-list.component.ts` | `$APP_FOLDER/Frontend/src/app/pages/<entity>/...` (overrides generated minimal list) |

## Why the DbContext overlay is seed-data-only

The fixture overrides **only** the demo seed data (`${APP_NAME}ApplicationDbContext.SeedData.cs`), never the DbContext class itself. The generated `${APP_NAME}ApplicationDbContext.cs` (constructor, `OnModelCreating`, `SaveChangesAsync`) is left untouched, and `SeedData` is wired as a `partial class` member that our overlay supplies.

This exists because the old overlay copied the *entire* DbContext class. When the framework changed `ApplicationDbContext<TUser>`'s constructor signature (adding `IExternalProviderSettings`), the init template was updated but the fixture's full-file copy drifted and CI failed with `CS1729: ... does not contain a constructor that takes 1 arguments`. A seed-only overlay carries no framework plumbing, so signature changes can never break it again. When you need different e2e seed data, edit only `ApplicationDbContext.SeedData.cs`.

## Overriding the generated list inside the fixture suite

`spiderly add-new-entity` generates a list component with a single numeric Id column plus Details/Delete actions (see `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` → `GetSpiderlyAngularTableTsTemplate`). To drive text / numeric / boolean filter tests, add a fixture component at `frontend/app/<entity>/<entity>-list.component.ts` with the columns you need, then add a copy step in `setup.sh`. The Product fixture (`tests/e2e-fixtures/frontend/app/product/product-list.component.ts`) is the reference — text, numeric, and boolean columns wired with the correct match-mode flags.

The *behavior* (generated lists are Id-only) is also documented in the `e2e-testing` consumer skill, but the override-via-fixture-overlay pattern is internal to this suite.
