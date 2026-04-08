## What is Spiderly

Spiderly is a .NET 9 + Angular 19 code generator. It reads EF Core entity classes decorated with custom attributes and generates: CRUD UI (Angular), API controllers, services, DTOs, mappers, FluentValidation rules, Angular validators, TypeScript entity classes, and more. Users extend generated base classes with custom logic.

Spiderly is a fast-moving startup — no backward compatibility needed. Make breaking changes freely.

### Versioning

`X.Y.Z` (stable) or `X.Y.Z-preview.N` (preview). All packages share the same version. Stored in each `.csproj` `<Version>` tag, `Angular/projects/spiderly/package.json`, and `spiderly-cli/package.json`.

## Documentation updates

When Spiderly code changes affect public API, attributes, generated output, or behavior — update the documentation in `spiderly-website/` (inside the PACMS workspace) accordingly.

## Coding conventions

- Prefer raw string literals (`$$""" """`) for multiline strings in C#
- Enum types follow the `...Codes` naming convention (e.g., `StatusCodes`, `UIControlTypeCodes`)
- `bool?` (nullable) is **recommended** for checkbox properties — non-nullable `bool` is supported but `bool?` is preferred in most cases. Treat `null` as `false` in business logic
- All generated methods that end users can use (virtual hooks, overridable methods) must have XML `<summary>` documentation with `<example>` showing usage
- **Database table names are singular** — matching the entity class name exactly (e.g., `Category` class → `"Category"` table, not `"Categories"`). This is because Spiderly registers entities via `modelBuilder.Entity()` without `DbSet<T>` properties, so EF Core uses the class name as-is

## AI-Agentic Philosophy

Spiderly is an AI-agentic framework. Every feature must be drivable by an AI agent without human intervention. See the `ai-agentic-design` skill (`claude-plugins/skills/ai-agentic-design/SKILL.md`) for the complete design principles. Key rules: non-interactive by default, fail loudly with non-zero exit codes, validate prerequisites upfront, Docker-first for infrastructure in non-interactive mode.
