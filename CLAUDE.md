## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Spiderly is a fast-moving startup — no backward compatibility needed. Make breaking changes freely.

## Config ↔ options binding is a reflective contract — guard it

`appsettings` is bound to options classes (`EmailOptions`, `JwtOptions`, …) **reflectively at runtime**, with no compile-time link. So a shape mismatch between the documented config and the options type binds **silently** to a default/empty value and only fails much later at first use. This actually shipped: `EmailSender` became a `{ Email, Name }` object in code, but the JSON schema + an existing consumer's `appsettings` still had it as a **string** → bound to an empty `EmailSender` (`Email == null`) → 500 "sender is missing" on the first login email, latent for weeks.

When you add/refactor an option:
- Update **all** of: the options class, the `spiderly init` template's emitted `appsettings`, **`schemas/appsettings.schema.json`**, and any consumer config. The schema and existing configs are the ones that silently drift.
- Add a **`ValidateOnStart` guard** in `StartupExtensions.AddSpiderly` for config that is *required when a feature is enabled* (mirror the `JwtKey` / `EmailSender.Email` checks) so a missing/empty value **fails loudly at boot**, not at first use. `ValidateOnStart` validates *values*, not *shape* — a wrong-shape binding produces an empty default and passes unless you assert the value.
- Lock the shape with a binding test in `Spiderly.Shared.Tests/OptionsBindingTests.cs` (bind a representative `appsettings` to the options, assert required fields populate).

### Versioning

`X.Y.Z` (stable) or `X.Y.Z-preview.N` (preview). All packages share the same version. Stored in each `.csproj` `<Version>` tag, `Angular/projects/spiderly/package.json`, and `spiderly-cli/package.json`. These are bumped together by `.github/workflows/release.yml` — do not hand-edit.

**Version bumps happen at publish time, not during refactors.** Don't bump the version as part of a feature or refactor PR — even for breaking changes. The human owns release cadence and decides when to cut a new version.

User-facing version upgrades (consumer apps moving from one Spiderly release to another) are handled by the `spiderly-upgrade` skill — see `claude-plugins/skills/spiderly-upgrade/SKILL.md`.

## Documentation updates

When Spiderly code changes affect public API, attributes, generated output, or behavior — update the documentation in the `spiderly-website/` sibling repo accordingly.

## API error codes

`ApiErrorCodes` (returned as `ApiErrorDTO.errorCode`) is a cross-language public contract. Three mirrors must stay in sync whenever a code is added, removed, or renamed:

1. `Spiderly.Shared/Contracts/ApiErrorCodes.cs` — canonical C# source.
2. `Angular/projects/spiderly/src/lib/errors/api-error-codes.ts` — admin consumers.
3. Downstream TS mirrors in any consuming app (e.g. a storefront's `api-error.ts`).

`ApiErrorCodes` lives under `Spiderly.Shared.Contracts` because it is a static constants class, not a DTO.

Changing `ApiErrorCodes` also changes the framework-metadata SSOT — regenerate it (see below) or CI fails.

## Framework metadata SSOT — regenerate after contract changes

`framework-metadata.json` (repo root), `claude-plugins/docs/*/references/*.generated.md`, and the type-zoo e2e fixture `tests/e2e-fixtures/backend/entities/ZooShapes.cs` (one entity property per supported shape axis, derived from `SpiderlyTypeRef.ScalarKindByName` by `Spiderly.ZooGenerator`) are **committed build artifacts** derived from code. CI regenerates them and fails on any diff. After changing any covered contract — `ApiErrorCodes`, `MatchModeCodes`, `UIControlTypeCodes`, `SecurityBaseController` endpoints, `Spiderly.Shared.Attributes.*`, Angular `helper-functions.ts` / `ValidatorAbstractService` / `spiderly-*` controls, or the shape-axis data in `SpiderlyTypeRef` — **including only editing their XML `<summary>` docs**, regenerate and commit the artifacts in the same commit:

```bash
tools/regen-metadata.sh
```

Never hand-edit the JSON or `.generated.md` files. Details: `docs/framework-metadata-ssot.md`.

A gated pre-commit hook (`.githooks/pre-commit`) automates this: when staged files touch SSOT sources it regenerates and auto-stages the artifacts, and it runs `TsContractMirrorTests` when the hand-maintained C#↔TS mirror files (`ApiErrorCodes`, `MatchModeCodes`) are staged. Behavior details: `docs/framework-metadata-ssot.md`. Activate it once per clone:

```bash
git config core.hooksPath .githooks
```

## Agent guidance bundle — regenerate after skill changes

`Angular/projects/spiderly/agent/` (`manifest.json` + `docs/**` + `skills/**`) is a **committed build artifact** that ships *inside* the `spiderly` npm package (via `ng-package.json` assets) so it lands version-pinned at `node_modules/spiderly/agent/` in consumer apps. `docs/**` is browsed via the `AGENTS.md` pointer; `skills/**` is junctioned into `.claude/skills`. `Spiderly.CLI agent-sync` reads it to project version-matched AI-agent guidance into a consumer (writes an `AGENTS.md` index, makes `CLAUDE.md` import it, and junctions `skill`-surface skills into `.claude/skills`). Design: `docs/agent-guidance-distribution.md`.

The bundle is derived from two authoring trees — `claude-plugins/docs/**` (reference docs, each `index.md`) and `claude-plugins/skills/**` (workflow skills, each `SKILL.md`). Which tree a folder lives in *is* the doc/skill split — there is no surface map. After changing any doc or skill, or its `*.generated.md` references, regenerate and commit the bundle in the same commit:

```bash
node tools/build-agent-bundle.mjs
```

Never hand-edit anything under `agent/`. The generator fails loud if a folder is missing its `index.md`/`SKILL.md` or its frontmatter `name` ≠ folder name (catches half-done renames). CI and the gated `.githooks/pre-commit` regenerate it and fail on any diff/untracked change, same model as the framework-metadata SSOT. Run the bundle regen **after** `tools/regen-metadata.sh` (it copies the freshly-generated reference tables).

## Coding conventions

- Prefer raw string literals (`$$""" """`) for multiline strings in C#
- Enum types are conventionally named with a `...Codes` suffix (e.g., `StatusCodes`, `UIControlTypeCodes`) — convention only, not enforced; `[SpiderlyEnum]` is what marks an enum for code generation
- `bool?` (nullable) is **recommended** for checkbox properties — non-nullable `bool` is supported but `bool?` is preferred in most cases. Treat `null` as `false` in business logic
- All public members in shipped packages (`Spiderly.Shared`, `Spiderly.Security`, `Spiderly.Infrastructure`) must have `/// <summary>` XML doc comments — never plain `//` comments as documentation. Generated methods that end users can override (virtual hooks) should also include `<example>` showing usage
- **Database table names are singular** — matching the entity class name exactly (e.g., `Category` class → `"Category"` table, not `"Categories"`). This is because Spiderly registers entities via `modelBuilder.Entity()` without `DbSet<T>` properties, so EF Core uses the class name as-is
- **Hand-written classes require classification attributes.** Source generators enroll classes by marker attribute, not by namespace suffix:
  - Entities → `[SpiderlyEntity]`
  - M2M junction classes → `[M2M]` **and** `[SpiderlyEntity]` (both required — `[M2M]` flags the junction; `[SpiderlyEntity]` enrolls it for generation)
  - Hand-written **standalone** DTOs → `[SpiderlyDTO]`. The generated declarations (`{Entity}DTO` / `{Entity}SaveBodyDTO` / `{Entity}MainUIFormDTO`) need no attribute — and neither does a hand-written `partial class {Entity}DTO` that merely **extends** one to add a property: such a partial is merged into the generated DTO by name (`PipelineFactory` enrolls it, `GetDTOClasses` merges it), so its members reach every artifact generator. `[SpiderlyDTO]` is required only for a brand-new DTO that does not extend a generated one.
  - Custom controllers → `[SpiderlyController]`
  - Entity services extending `{Entity}ServiceGenerated` → `[SpiderlyService]`
  - The hand-written partial mapper class → `[SpiderlyDataMapper]`
  - C# enums and class-based enums (static classes of string constants) exposed to Angular → `[SpiderlyEnum]`

## Bundle co-required registrations behind one call

When two or more registrations are **co-required** — the app is silently broken if any is present without the others — register them behind **one call** so they can't drift apart. Don't expose them as independent `Add*` steps a consumer must remember to pair; a forgotten one fails at runtime in a confusing way (the permission-policy handler being a separate `AddSpiderlyAuthorization` call from the policy provider 403'd *every* permission-gated endpoint, silently). Precedents: `AddBrevoEmailing` (emailing service + its named HttpClient), `AddSpiderlyApiKeyAuthentication` (authenticator + scheme + policy scheme + default-scheme config), and `AddSecurity<TUser, TUserExternalLogin, TAuthorizationService>` (the whole auth core; API keys opt in via the `SpiderlySecurityBuilder` sub-builder).

- Keep **genuinely-optional** pieces opt-in (a sub-builder or a separate `Add*`), so apps that don't use them never have to name their types — don't over-merge.
- Use **`TryAdd`** inside the bundle so a consumer can still pre-register a custom implementation (override seam).
- When a one-way **assembly boundary** forces the pairing apart (e.g. `AddSpiderly` in `Spiderly.Shared` registers the permission policy provider, but the handler lives in `Spiderly.Security`), you can't merge them — so add a **fail-loud boot guard** for the unavoidable seam (see `PermissionHandlerRegistrationGuard`), mirroring the `ValidateOnStart` philosophy: never let a missing half-of-a-pair degrade silently.

## Init template drift

`Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` holds the full project template emitted by `spiderly init` — `Startup.cs`, `AppServiceExtensions.cs`, the entity scaffolding, package.json, etc. — as raw string literals. When you change a framework public API (DI registration shape, `SpiderlyBuilder` methods, generated service constructor signature, new built-in service that needs registering), audit the relevant template strings in this file and update them too. CI's e2e job catches the worst regressions, but only for code paths the fixture exercises (commit `96ad6b9` removed the global `IFileManager` slot but missed adding `services.AddTransient<DiskStorageService>()` to the template — every freshly-init'd app crashed on the first save of a `[DiskStorage]` property).

## `.spiderly/` project config

Spiderly's per-project config lives under `.spiderly/` at the app root (replaces the former root-level `spiderly.json`; `spiderly-upgrade` migrates existing apps):

- **`.spiderly/config.json`** (committed) — the `SpiderlyConfig` read by the source generators via `AdditionalFiles`: `generators` (per-generator enable/disable, default on via `IsGeneratorEnabled`) and `api.routePrefix`. Register it in the generating project's csproj: `<AdditionalFiles Include=".spiderly/config.json" />`. The matcher (`Extensions.GetSpiderlyConfig`) keys on a path ending in `.spiderly/config.json` (separator-normalized for Windows + POSIX).
- **`.spiderly/config.local.json`** (gitignored via `**/.spiderly/*.local.json`) — machine-local overrides. Today: `agentSync.root`, the workspace/umbrella dir `spiderly agent-sync` projects guidance into when the AI agent runs from a root that nests this app. Set it with `spiderly agent-sync --agent-root <dir> --save`; bare runs (including the one inside `spiderly-upgrade`) then reuse it. Machine-local because it encodes one developer's directory layout — committing it would impose that layout on every other consumer of the app repo.

## Regression tests must fail on the commit that adds them

If you write a regression test **for a specific bug**, that test **must demonstrably fail on its commit and pass on the immediately-following fix commit** — never the reverse, never both in one commit. A green-on-its-own-commit regression test is a placebo: it codifies the bug's existence without proving the suite actually catches it. The nested-O2M dropdown regression test (`8a2714f`) was authored aspirationally — added without the matching generator fix — and a separate `if (!setupVar) test.skip()` retry-mask kept CI green for a month while the underlying bug existed. The discipline: add the test, watch it fail in CI, then push the fix.

Corollary: never write `if (!setupVar) test.skip()` guards. Either seed the variable in a `beforeAll` (which is preserved across Playwright retries) or let the test fail loudly with a clear assertion error. Skip guards on missing setup state silently convert consistent failures into "flaky → exit 0" CI passes.

**A new DETECTOR is validated by a negative control instead** — harnesses, gates, lint rules, diagnostics: anything whose job is to find a class of problem rather than to pin one known defect. Its red is not evidence about itself. If it lands red on some pre-existing bug you can't tell whether the detector works and found something or is miswired and failed for its own reasons, and a broad detector has no narrow assertion to make that legible. So land it **green**, and add a test that feeds it input known to be bad and asserts it fires (`GeneratedCodeCompilationTests.NegativeControl_ABrokenEntityIsReported`). That proves it isn't a placebo on every run, not once in history. Pair it with an assertion that it actually inspected something — "zero problems found" must not also pass when the detector silently examined nothing.

Both rules can apply to one batch, in this order: fix the bugs the detector found as their own red-then-fix pairs, then land the detector green on top.

## AI-Agentic Philosophy

Spiderly is an AI-agentic framework. Every feature must be drivable by an AI agent without human intervention. See the `ai-agentic-design` skill (`.claude/skills/ai-agentic-design/SKILL.md` — contributor-only, intentionally kept out of the consumer-shipped `claude-plugins/skills/`) for the complete design principles. Key rules: non-interactive by default, fail loudly with non-zero exit codes, validate prerequisites upfront, Docker-first for infrastructure in non-interactive mode.
