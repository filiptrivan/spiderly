## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Spiderly is a fast-moving startup — no backward compatibility needed. Make breaking changes freely.

### Versioning

`X.Y.Z` (stable) or `X.Y.Z-preview.N` (preview). All packages share the same version. Stored in each `.csproj` `<Version>` tag, `Angular/projects/spiderly/package.json`, `spiderly-cli/package.json`, and `.claude-plugin/marketplace.json` (`plugins[0].version`). These are bumped together by `.github/workflows/release.yml` — do not hand-edit.

**Version bumps happen at publish time, not during refactors.** Don't bump the version as part of a feature or refactor PR — even for breaking changes. The human owns release cadence and decides when to cut a new version.

## Documentation updates

When Spiderly code changes affect public API, attributes, generated output, or behavior — update the documentation in the `spiderly-website/` sibling repo accordingly.

## API error codes

`ApiErrorCodes` (returned as `ApiErrorDTO.errorCode`) is a cross-language public contract. Three mirrors must stay in sync whenever a code is added, removed, or renamed:

1. `Spiderly.Shared/Contracts/ApiErrorCodes.cs` — canonical C# source.
2. `Angular/projects/spiderly/src/lib/errors/api-error-codes.ts` — admin consumers.
3. Downstream TS mirrors in any consuming app (e.g. a storefront's `api-error.ts`).

`ApiErrorCodes` lives under `Spiderly.Shared.Contracts` because it is a static constants class, not a DTO.

## Coding conventions

- Prefer raw string literals (`$$""" """`) for multiline strings in C#
- Enum types are conventionally named with a `...Codes` suffix (e.g., `StatusCodes`, `UIControlTypeCodes`) — convention only, not enforced; `[SpiderlyEnum]` is what marks an enum for code generation
- `bool?` (nullable) is **recommended** for checkbox properties — non-nullable `bool` is supported but `bool?` is preferred in most cases. Treat `null` as `false` in business logic
- All public members in shipped packages (`Spiderly.Shared`, `Spiderly.Security`, `Spiderly.Infrastructure`) must have `/// <summary>` XML doc comments — never plain `//` comments as documentation. Generated methods that end users can override (virtual hooks) should also include `<example>` showing usage
- **Database table names are singular** — matching the entity class name exactly (e.g., `Category` class → `"Category"` table, not `"Categories"`). This is because Spiderly registers entities via `modelBuilder.Entity()` without `DbSet<T>` properties, so EF Core uses the class name as-is
- **Hand-written classes require classification attributes.** Source generators enroll classes by marker attribute, not by namespace suffix:
  - Entities → `[SpiderlyEntity]`
  - M2M junction classes → `[M2M]` **and** `[SpiderlyEntity]` (both required — `[M2M]` flags the junction; `[SpiderlyEntity]` enrolls it for generation)
  - Hand-written DTOs → `[SpiderlyDTO]` (generated DTOs like `{Entity}DTO` / `{Entity}SaveBodyDTO` / `{Entity}MainUIFormDTO` need no attribute)
  - Custom controllers → `[SpiderlyController]`
  - Entity services extending `{Entity}ServiceGenerated` → `[SpiderlyService]`
  - The hand-written partial mapper class → `[SpiderlyDataMapper]`
  - C# enums and class-based enums (static classes of string constants) exposed to Angular → `[SpiderlyEnum]`

## Init template drift

`Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` holds the full project template emitted by `spiderly init` — `Startup.cs`, `AppServiceExtensions.cs`, the entity scaffolding, package.json, etc. — as raw string literals. When you change a framework public API (DI registration shape, `SpiderlyBuilder` methods, generated service constructor signature, new built-in service that needs registering), audit the relevant template strings in this file and update them too. CI's e2e job catches the worst regressions, but only for code paths the fixture exercises (commit `96ad6b9` removed the global `IFileManager` slot but missed adding `services.AddTransient<DiskStorageService>()` to the template — every freshly-init'd app crashed on the first save of a `[DiskStorage]` property).

## AI-Agentic Philosophy

Spiderly is an AI-agentic framework. Every feature must be drivable by an AI agent without human intervention. See the `ai-agentic-design` skill (`claude-plugins/skills/ai-agentic-design/SKILL.md`) for the complete design principles. Key rules: non-interactive by default, fail loudly with non-zero exit codes, validate prerequisites upfront, Docker-first for infrastructure in non-interactive mode.
