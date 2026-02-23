# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Docs: https://www.spiderly.dev/docs

Spiderly is a fast-moving startup — no backward compatibility needed. Make breaking changes freely.

## Project structure

```
spiderly/
├── Spiderly.Shared/          # Core: attributes (44), base entities, DTOs, enums, interfaces, helpers
├── Spiderly.SourceGenerators/ # IIncrementalGenerator-based code generators (netstandard2.0)
│   ├── Net/                   # 10 .NET generators (controllers, services, DTOs, validators, mappers, etc.)
│   ├── Angular/               # 5 Angular generators (entities, base-details, controllers, validators, enums)
│   ├── Shared/                # Helpers.cs (core reflection/analysis), Extensions.cs, Validations.cs
│   ├── Models/                # SpiderlyClass, SpiderlyProperty, SpiderlyAttribute, etc.
│   └── Enums/                 # UIControlTypeCodes, CrudCodes, NamespaceExtensionCodes, etc.
├── Spiderly.Infrastructure/   # ApplicationDbContext<TUser>, M2M/M2O config, concurrency, audit tracking
├── Spiderly.Security/         # JWT auth, Google OAuth, token management, permission-based authorization
├── Spiderly.CLI/              # `spiderly` CLI tool (init, add-new-entity, migrations)
├── Angular/                   # Angular library (npm: "spiderly")
│   └── projects/spiderly/     # UI controls, layout, data table, auth, forms, interceptors, guards
└── Spiderly.sln
```

## Build commands

```bash
# .NET — build all projects
dotnet build                                    # from repo root
dotnet build --configuration Release            # release build

# .NET — run tests
dotnet test                                     # from repo root

# .NET — check formatting
dotnet format --verify-no-changes

# Angular library — install & build
cd Angular && npm ci
npm run build -- spiderly --configuration production

# Angular — lint
cd Angular && npm run lint

# CLI — local install for testing
# Run Spiderly.CLI/cli-local-pack.ps1
```

## Architecture deep dive

### Source generators

All 15 generators implement `IIncrementalGenerator` (Roslyn incremental source generation). They:
1. Discover entity classes in the `*.Entities` namespace that inherit `BusinessObject<T>` or `ReadonlyObject<T>`
2. Read attributes on those entities/properties via Roslyn syntax analysis
3. Generate `*.generated.cs` files (.NET) and `*.generated.ts` files (Angular) using CodegenCS
4. Generated files are written to the consumer's project — Spiderly itself doesn't contain generated output

Key shared logic lives in `Spiderly.SourceGenerators/Shared/Helpers.cs` (~1,566 lines) — entity discovery, property analysis, base class resolution, attribute extraction.

The `SourceGenerators` project targets **netstandard2.0** (Roslyn requirement) and ships as an analyzer in the NuGet package.

### Attribute-driven generation

Entities are decorated with attributes from `Spiderly.Shared/Attributes/`:
- **Entity attributes**: `M2M`, `M2MWithMany`, `WithMany`, `CascadeDelete`, `SetNull`, `DisplayName`, `DoNotAuthorize`, `Controller`
- **UI attributes**: `UIControlType`, `UIControlWidth`, `UIDoNotGenerate`, `UIPanel`, `UIPropertyBlockOrder`, `UIOrderedOneToMany`, `UITableColumn`
- **Validation attributes**: `GreaterThanOrEqual`, `CustomValidator`, `AcceptedFileTypes`, `MaxFileSize`, `ImageWidth`, `ImageHeight`
- **Storage attributes**: `BlobName`, `S3Url`, `S3PublicUrl`, `CloudinaryPublicId`
- **Translations**: JSON files in `{Shared}/Translations/` (not attributes). Auto-scaffolded by `TranslationsGenerator`.

### Base entities

- `BusinessObject<T>` (T = long/int/byte) — Id, Version (optimistic concurrency via `[ConcurrencyCheck]`), CreatedAt, ModifiedAt
- `ReadonlyObject<T>` — for lookup/reference tables (no CRUD generation, no timestamps)

### Angular library

Published as npm package "spiderly". Provides:
- 13+ UI control components (autocomplete, calendar, checkbox, dropdown, editor, file, multiselect, etc.) wrapping PrimeNG
- Layout system (sidebar, topbar, profile avatar)
- Data table and data view components with pagination/filtering
- Auth system (login, JWT interceptor, auth guard, Google sign-in)
- Base form class, form controls, validation service
- HTTP interceptors (loading spinner, JWT, unauthorized redirect, JSON parser)
- Localization via `@jsverse/transloco` (EN + SR-Latn-RS)

### Release pipeline

Versioning: `X.Y.Z` (stable) or `X.Y.Z-preview.N` (preview). All packages share the same version.

Published artifacts:
- **NuGet**: Spiderly.Shared, Spiderly.SourceGenerators, Spiderly.Security, Spiderly.Infrastructure, Spiderly.CLI
- **npm**: spiderly (Angular library)

Version is stored in each `.csproj` `<Version>` tag and `Angular/projects/spiderly/package.json`.

## Documentation updates

When Spiderly code changes affect public API, attributes, generated output, or behavior — update the documentation in `spiderly-website/` (inside the PACMS workspace) accordingly.

## Coding conventions

- Don't use `var` unless it's an anonymous type
- Add comments only for hacks/non-obvious workarounds and for documentation (XML `<summary>`). Never place `//` comments above methods — use `<summary>` or nothing
- Don't delete existing comments
- Reference types aren't nullable in .NET (C#)
- If Method A calls Method B, list Method A first, then Method B
- Prefer existing available methods over creating new ones
- Split logic into smaller, focused methods whenever possible
- Prefer raw string literals (`$$""" """`) for multiline strings in C#
- Enum types follow the `...Codes` naming convention (e.g., `StatusCodes`, `UIControlTypeCodes`)
- Use `List<T>` for one-to-many and many-to-many collections, not `IList<T>` or other interfaces
- Always initialize lists immediately with `new()`
- Prefer shorthand `Class c = new();` — but only when the constructor has no parameters. With parameters, use object initializer syntax
- `[StringLength(X)]` without `MinimumLength` generates an **exact length** validation. Use `[StringLength(X, MinimumLength = Y)]` for a range
- All generated methods that end users can use (virtual hooks, overridable methods) must have XML `<summary>` documentation with `<example>` showing usage
