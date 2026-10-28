# Coding standards

The rules a reviewer judges from a change in Spiderly or in its website, spiderly-website, which
keeps none of its own. A rule a lint or test pins is named by its pin.

## Design (Filip, 2026-10-25)

We work in this codebase for years, so a diff is *strategic* programming, never *tactical*
(Ousterhout, *A Philosophy of Software Design*): working code is the floor, and the code a diff
touches is left simpler than it was. A conforming diff puts new behaviour where the design absorbs
it, a deep module or an existing seam; a special case, flag, copied block or workaround patched
around the design is a finding, even when its tests pass.

## Code and comments are written for agents

Premise: Spiderly is a framework for agents, and its contributors work in it through agents (Filip,
2026-10-25), so documentation is written for its actual reader: a stateless agent with grep and a
token budget. A conforming diff:

- anchors with symbols, never line numbers (`see PermissionHandlerRegistrationGuard`, not
  `see line 82`);
- tells each fact once, with pointers everywhere else; the code is itself a telling, so no prose
  restates what it does;
- dates a claim and names its evidence ("measured 2026-08-07" is judgeable for staleness,
  "currently" is not);
- pins a rule with a test or lint rule rather than prose;
- never narrates syntax or softens for an imagined junior.

## C# conventions

- Multiline strings are raw string literals (`$$""" """`).
- Enum types take a `...Codes` suffix (`StatusCodes`, `UIControlTypeCodes`), a convention nothing
  enforces: `[SpiderlyEnum]` is what marks an enum for code generation.
- A checkbox property is `bool?`, `null` read as `false` in business logic; a non-nullable `bool`
  is supported and is the exception.
- Every public member of a shipped package (`Spiderly.Shared`, `Spiderly.Security`,
  `Spiderly.Infrastructure`) carries a `/// <summary>` XML doc comment, never a plain `//` comment
  as its documentation, and a generated method an end user can override (a virtual hook) carries an
  `<example>` of its use too. The pin covers only the contracts the metadata exporter exports, which
  fails on a missing `<summary>` (`docs/framework-metadata-ssot.md`); the shipped projects suppress
  CS1591.
- **Database table names are singular**, the entity class name exactly (`Category` class →
  `"Category"` table, not `"Categories"`): Spiderly registers entities via `modelBuilder.Entity()`
  without `DbSet<T>` properties, so EF Core uses the class name as-is.
- **A hand-written class carries its classification attribute**, because the source generators
  enroll a class by marker attribute, not by namespace suffix:
  - Entities → `[SpiderlyEntity]`
  - M2M junction classes → `[M2M]` **and** `[SpiderlyEntity]` (both required — `[M2M]` flags the
    junction; `[SpiderlyEntity]` enrolls it for generation)
  - Hand-written **standalone** DTOs → `[SpiderlyDTO]`. The generated declarations
    (`{Entity}DTO` / `{Entity}SaveBodyDTO` / `{Entity}MainUIFormDTO`) need no attribute — and
    neither does a hand-written `partial class {Entity}DTO` that merely **extends** one to add a
    property: such a partial is merged into the generated DTO by name (`PipelineFactory` enrolls
    it, `GetDTOClasses` merges it), so its members reach every artifact generator. `[SpiderlyDTO]`
    is required only for a brand-new DTO that does not extend a generated one.
  - Custom controllers → `[SpiderlyController]`
  - Entity services extending `{Entity}ServiceGenerated` → `[SpiderlyService]`
  - The hand-written partial mapper class → `[SpiderlyDataMapper]`
  - C# enums and class-based enums (static classes of string constants) exposed to Angular →
    `[SpiderlyEnum]`

## Options binding

`appsettings` binds to its options classes (`EmailOptions`, `JwtOptions`, …) **reflectively at
runtime**, with no compile-time link, so a shape the documented config and the options type disagree
on binds **silently** to an empty default and fails only at first use (the `EmailSender` incident:
the summary on `OptionsBindingTests`). A diff that adds or reshapes an option:

- changes **all** of the options class, the `spiderly init` template's emitted `appsettings`,
  **`schemas/appsettings.schema.json`** and any consumer config together; the schema and the
  existing configs are the ones that drift silently;
- adds a **`ValidateOnStart` guard** in `StartupExtensions.AddSpiderly` for config *required when a
  feature is enabled*, mirroring the `JwtKey` / `EmailSender.Email` checks, so a missing or empty
  value **fails loudly at boot**, not at first use. `ValidateOnStart` validates *values*, not
  *shape*: a wrong-shape binding produces an empty default and passes unless the guard asserts the
  value;
- locks the shape with a binding test in `Spiderly.Shared.Tests/OptionsBindingTests.cs`: a
  representative `appsettings` bound to the options, the required fields asserted populated.

## Bundle co-required registrations behind one call

Registrations are **co-required** when the app is silently broken with any one of them present
without the others. A conforming diff registers them behind **one call**, never as independent
`Add*` steps a consumer must remember to pair: a forgotten one fails at runtime in a confusing way
(the permission-policy handler being a separate `AddSpiderlyAuthorization` call from the policy
provider 403'd *every* permission-gated endpoint, silently). Precedents: `AddBrevoEmailing`
(emailing service + its named HttpClient), `AddSpiderlyApiKeyAuthentication` (authenticator +
scheme + policy scheme + default-scheme config), and
`AddSecurity<TUser, TUserExternalLogin, TAuthorizationService>` (the whole auth core; API keys opt
in via the `SpiderlySecurityBuilder` sub-builder).

- **Genuinely optional** pieces stay opt-in (a sub-builder or a separate `Add*`), so apps that do
  not use them never have to name their types; the bundle does not over-merge.
- The bundle registers with **`TryAdd`**, so a consumer can still pre-register a custom
  implementation (the override seam).
- Where a one-way **assembly boundary** forces the pairing apart (`AddSpiderly` in
  `Spiderly.Shared` registers the permission policy provider, but the handler lives in
  `Spiderly.Security`), the unavoidable seam gets a **fail-loud boot guard** instead, as
  `PermissionHandlerRegistrationGuard` does (pinned by `PermissionHandlerRegistrationGuardTests`):
  a missing half of a pair never degrades silently, the `ValidateOnStart` philosophy.

## Init template drift

`Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` holds the full project template `spiderly
init` emits (Startup.cs, AppServiceExtensions.cs, the entity scaffolding, package.json, …) as raw
string literals. A diff that changes a framework public API (DI registration shape, `SpiderlyBuilder`
methods, a generated service's constructor signature, a new built-in service that needs
registering) updates the template strings it affects too. CI's e2e job catches the worst
regressions, but only for code paths the fixture exercises: commit `96ad6b9` removed the global
`IFileManager` slot but missed adding `services.AddTransient<DiskStorageService>()` to the
template, and every freshly init'd app crashed on the first save of a `[DiskStorage]` property.

## Versions

A diff leaves every version as it is, a breaking change's included: `.github/workflows/release.yml`
bumps all packages together at publish time, and the maintainer owns the release cadence.

## Tests

- **A regression test for a specific bug fails on its own commit and passes on the fix commit right
  after it** — never the reverse, never both in one commit. A regression test green on its own
  commit is a placebo: it codifies the bug's existence without proving the suite catches it. The
  nested-O2M dropdown regression test (`8a2714f`) was authored aspirationally, without the matching
  generator fix, and a separate `if (!setupVar) test.skip()` retry-mask kept CI green for a month
  while the bug existed. The discipline: add the test, watch it fail in CI, then push the fix.
- **No `if (!setupVar) test.skip()` guard.** The variable is seeded in a `beforeAll` (preserved
  across Playwright retries), or the test fails loudly with a clear assertion error: a skip guard on
  missing setup state silently converts a consistent failure into a "flaky → exit 0" CI pass.
- **A new detector lands green, validated by a negative control** — a harness, gate, lint rule or
  diagnostic: anything whose job is to find a class of problem rather than pin one known defect.
  Its red is no evidence about itself: landed red on a pre-existing bug, nobody can tell whether it
  works and found something or is miswired and failed for its own reasons, and a broad detector has
  no narrow assertion to make that legible. Its diff adds a test that feeds it input known to be
  bad and asserts it fires (`GeneratedCodeCompilationTests.NegativeControl_ABrokenEntityIsReported`),
  proof on every run, not once in history, that it is no placebo, and an assertion that it inspected
  something, so "zero problems found" cannot also pass when it examined nothing.
- Both rules can apply to one batch, in this order: the bugs the detector found land as their own
  red-then-fix pairs, then the detector lands green on top.
- **A change to a source generator or the EF model-config extensions is exercised by a fixture
  shape, and the e2e is green in CI before it ships**; snapshot tests are necessary but never
  sufficient (why, and the five bugs only the e2e caught: `tests/e2e-fixtures/CLAUDE.md`). The diff
  adds or extends a fixture entity when the shape or composition the change touches is missing from
  the zoo; when existing fixtures already exercise the changed path, a green e2e suffices: a fixture
  entity added per change regardless of coverage is ritual, and ritual is how aspirational tests
  happen (`8a2714f`, above).
- **A model test that exercises a relationship runs the same `Configure*Relationships` passes in the
  same order as the real `ApplicationDbContext.OnModelCreating`**, or it misses inter-pass NREs
  (`OneToOneRelationshipModelTests`, whose summary tells the one it missed).

## Angular library

The published `spiderly` package, `Angular/projects/spiderly`. Its rules are path-scoped, each
loaded with the files it governs:

- Library SCSS (content PrimeNG teleports to `document.body`, the viewport inset, scroll
  behaviour): `.claude/rules/library-scss.md`.
- Static text in a library template, and seeding its key: `.claude/rules/library-template-text.md`.
- The runtime `@angular/*` pins and the lockfile: `.claude/rules/angular-dependencies.md`.
