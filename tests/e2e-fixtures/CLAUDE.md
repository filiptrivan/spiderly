# E2E Fixtures (Playwright)

This file covers the **framework-internal** overlay mechanics that turn `tests/e2e-fixtures/` into Spiderly's own CI suite. For test-authoring patterns that also apply to consumer apps (login helper, PrimeNG v19 selectors, trace debugging, data seeding), see the `e2e-testing` consumer doc at `claude-plugins/docs/e2e-testing/index.md`.

## Why a codegen change is not "done" until the e2e is green

Generator unit tests (`Spiderly.SourceGenerators.Tests`) snapshot the **generated text**; since 2026-07-30 (spiderly `e9d7e15e`) `GeneratedCodeCompilationTests` also **compiles** every .NET generator's output, but nothing there **runs** it, and all of it is a **single compilation** with no cross-project *referenced* entities, so `ControllerGenerator`, which reads referenced assemblies, stays outside it (checked 2026-10-28). So a whole class of bug is invisible to them and **only** this e2e (which builds, migrates, boots, and drives a real `spiderly init` app on real Postgres) catches it. Adding native one-to-one support, before that compile test existed, surfaced five such bugs, none of which broke a single snapshot test:

- Generated C# that references a member the DTO doesn't have (`dto.{Nav}Id` on a side that owns no FK) → CS1061.
- A `long?`/`long` mismatch in a generated query (`List<long>.Contains(x.{nullableFk})`) → CS1503.
- A **multi-project** inconsistency: a `.WebAPI` generator iterates entities as *referenced* classes, so a flag set only on *current-project* classes left two generators disagreeing → CS1061. The single-compilation unit harness **cannot** reproduce this.
- A runtime `NullReferenceException` in the EF model-config pass that only fires when the **full** `OnModelCreating` runs (the isolated model test missed it).
- A new entity with no seeded permissions → 403 at runtime.

The rule a generator or model-config diff follows (a fixture shape exercises it, the e2e green in CI before it ships), and how a model test orders its relationship passes: `CODING_STANDARDS.md` → "Tests".

**Coverage is judged per COMPOSITION (control type × container context), not per feature.** Every feature existing *somewhere* in the zoo proves nothing about their products: for months `Project.Members` covered multi-control M2M *on a standalone form* and `Project.ProjectTasks` covered ordered children's *scalars*, while the composition — a multi-control M2M **on an ordered child**, saved through the parent, the only path the inline UI has — existed nowhere, and its selections were silently dropped in production (the PACMS `IntegrationRuleGroup.Brands` regression; fixed by `ProjectTask.Watchers` + the ordered-path spec in `project-task-crud.spec.ts`). Container contexts to think in: standalone form / ordered child / nested ordered child. Note the ordered-path spec saves through the PARENT's endpoint — the standalone child endpoint tests above it do not touch the ordered pipeline at all. Post-fix nuance so this paragraph doesn't breed the ritual the one above disavows: the fix made the ordered save delegate to the child's standalone save, which *collapsed* the control-type × ordered-child product — every control now rides one code path by construction, so `Watchers` (one MultiSelect cell) is a **canary against future de-delegation**, not the first entry of a matrix to fill in. Multiply composition fixtures only where the generator actually branches per composition.

**Coverage is also judged per OPERATION on a mutable collection: add / removal** — an explicit empty list IS the removal case; "clear" is not a third cell to fill in. An add-leg assert says nothing about removal: the 2026-08-06 multiselect deselect regression (`c4e126bc`) shipped while every add-path check was green, because remove was uncovered at every layer at once (this suite's ordered-path spec asserted add-only; the Angular library had no control spec at all). The layers pin different halves and fail independently — that regression was purely client-side, invisible to any API-level spec here. API legs pin the backend's removal semantics; the library's Karma specs pin the control-to-form commit; the seam they meet at is the request body, which is why a UI-driven leg was deliberately not added. Whether omission-clears (the removal leg's NOTE in `project-task-crud.spec.ts`) is the *intended* API contract is an open design question.

## How the fixture overlay plugs into CI

```
spiderly init        → creates a generic test app at $APP_FOLDER
add-new-entity X     → generates minimal entity scaffolding (lists, details, etc.)
setup.sh APP_NAME APP_FOLDER  → overlays our fixtures on top of the generated app
dotnet build / ng build      → built with the overlay applied
playwright test              → exercises the resulting app
```

`setup.sh` copies fixture files into the generated app *after* CLI generation, so any folder structure you add here must mirror the target path inside `$APP_FOLDER`. Entity files are **enumerated one `cp` per file, NOT globbed**: a new fixture entity that is not added to `setup.sh` is silently absent, and the e2e fails on whatever references it.

## Why the DbContext overlay is seed-data-only

The old overlay copied the *entire* DbContext class: when the framework changed `ApplicationDbContext<TUser>`'s constructor signature (adding `IExternalProviderSettings`), the init template was updated but the fixture's full-file copy drifted and CI failed with `CS1729: ... does not contain a constructor that takes 1 arguments`. When you need different e2e seed data, edit only `ApplicationDbContext.SeedData.cs`.

## Overriding the generated list inside the fixture suite

`spiderly add-new-entity` generates a list component with a single numeric Id column, a filter store declaring just the `id` filter, plus Details/Delete actions (see `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` → `GetSpiderlyAngularTableTsTemplate`). To drive text / numeric / boolean filter tests, add a fixture component at `frontend/app/<entity>/<entity>-list.component.ts` with the columns *and matching store entries* you need, then add a copy step in `setup.sh`. The Product fixture (`tests/e2e-fixtures/frontend/app/product/product-list.component.ts`) is the reference — text, numeric, and boolean columns each backed by a store filter (the fixture overlays only the `.ts`; the generated `.html`'s `[filters]="filters"` binding is what renders them on the bar).
