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

Example:

```bash
cd MyApp/Backend
dotnet ef migrations add AddOrderNumberToProduct --project MyApp.Infrastructure --startup-project MyApp.Migrations
```

Always review the generated migration file before proceeding.

## Relationship schema is declarative — let the migration reflect it

Schema produced by relationship attributes (`[WithMany]` FKs, `[M2M]` join tables, `[WithOne]` one-to-one) is configured in the model (`OnModelCreating` extensions), so the migration just **reflects** it — you don't hand-write that DDL. In particular, a **one-to-one (`[WithOne]`) auto-emits a unique index on the FK**: do **not** add `[Index(IsUnique = true)]` yourself, and don't hand-write a `CREATE UNIQUE INDEX`. For an *optional* 1-1 (nullable FK) the index must allow many NULLs — that's left to provider conventions (Postgres `NULLS DISTINCT`, SQL Server's auto `IS NOT NULL` filter), so never add a `HasFilter`. If the generated migration shows a filtered/NULL-collapsing unique index on a nullable 1-1 FK, something is wrong — investigate rather than editing the migration by hand.

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

Each Spiderly solution includes a lightweight console app (e.g., `MyApp.Migrations`) specifically for running EF tooling. It exists because the main WebAPI project's DLLs are often locked by the running dev server. Always use it as the `--startup-project` for `dotnet ef` commands.
