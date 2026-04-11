---
name: ef-migrations
description: Create and apply EF Core migrations in Spiderly projects. Use when adding, modifying, or removing entity properties, changing column types, renaming columns, or any database schema change that requires a migration.
---

# EF Migrations

## Creating a migration

Run from the Backend directory, using the **Migrations console project** as startup (avoids DLL locking — no need to stop the running backend):

```bash
dotnet ef migrations add <MigrationName> --project <InfrastructureProject> --startup-project <MigrationsProject>
```

Example for PACMS:

```bash
cd pa-cms/Backend
dotnet ef migrations add AddOrderNumberToProduct --project PACMS.Infrastructure --startup-project PACMS.Migrations
```

Always review the generated migration file before proceeding.

## Applying locally

After creating a migration, always apply it to your local database:

```bash
dotnet ef database update --project <InfrastructureProject> --startup-project <MigrationsProject>
```

## Data migrations

When a schema change requires data conversion (e.g., converting 0 to NULL after making a column nullable), add `migrationBuilder.Sql()` calls inside the `Up` method. Keep them simple and idempotent.

## Production

Schema changes are applied to production **automatically through the deployment pipeline**. Never run DDL (ALTER TABLE, DROP COLUMN, CREATE INDEX, etc.) directly against the production database.

Direct production DB access (via db-query or psql) is only for **data queries and data updates** (SELECT, INSERT, UPDATE of row values) — not for schema changes.

## The Migrations project

Each Spiderly solution includes a lightweight console app (e.g., `PACMS.Migrations`) specifically for running EF tooling. It exists because the main WebAPI project's DLLs are often locked by the running dev server. Always use it as the `--startup-project` for `dotnet ef` commands.
