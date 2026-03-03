---
name: migration-workflow
description: Create and manage EF Core migrations using the Spiderly CLI. Use when creating or applying migrations, making schema changes (adding/removing entities or properties), removing a bad migration, or asking about dotnet ef commands.
---

# Migration Workflow

## CLI Commands

**Always use `spiderly`, never `dotnet ef` directly.** The CLI finds the correct `.csproj` paths automatically.

| Command | Purpose |
|---|---|
| `spiderly add-migration <Name>` | Create a new migration |
| `spiderly update-database` | Apply all pending migrations |
| `spiderly remove-migration` | Remove the last unapplied migration |
| `spiderly list-migrations` | List all migrations |

Run from anywhere inside the project (CLI auto-locates `*.Infrastructure.csproj` and `*.WebAPI.csproj`).

## What Needs a Migration?

| Change | Migration needed? |
|---|---|
| Add/remove entity class | Yes |
| Add/remove/rename property | Yes |
| Change property type | Yes |
| Change `[StringLength]` | Yes |
| Change `[Required]` | Yes |
| Add/remove `[CascadeDelete]`/`[SetNull]` | Yes |
| Change `[DisplayName]` | No (UI only) |
| Change `[UIControlType]` | No (UI only) |
| Change `[UIControlWidth]` | No (UI only) |
| Add/remove `[UIDoNotGenerate]` | No (UI only) |
| Change `[UIOrderedOneToMany]` | No (UI only) |
| Add/remove `[BlobName]` | No (mapped to existing string column) |
| Change `[AcceptedFileTypes]`/`[MaxFileSize]` | No (validation only) |
| Add/remove `[DoNotAuthorize]` | No (authorization only) |
| Change `[Controller]` | No (routing only) |

## Naming Convention

Use PascalCase describing the change:

```bash
spiderly add-migration AddProductTable
spiderly add-migration AddSalePriceToProductVariant
spiderly add-migration RemoveIsActiveFromCategory
spiderly add-migration ChangeSkuLengthOnProductVariant
```

## Troubleshooting

**Build errors before migration:** Fix all build errors first — `spiderly add-migration` runs `dotnet build` internally.

**Wrong migration generated:** Remove it and try again:

```bash
spiderly remove-migration
# fix the entity
spiderly add-migration CorrectName
```

**Migration already applied to DB:** You cannot `remove-migration` after `update-database`. Create a new migration that reverses the change instead.
