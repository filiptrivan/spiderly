using CaseConverter;
using Spiderly.Shared.Classes;
using Spiderly.Shared.Enums;

namespace Spiderly.Shared.Helpers.Scaffold
{
    /// <summary>
    /// The root folder of the project <c>spiderly init</c> scaffolds: the VS Code settings, the repository files,
    /// and <c>Frontend/</c> and <c>Backend/</c>.
    /// </summary>
    internal static class ProjectRootTemplate
    {
        internal static SpiderlyFolder Folder(ProjectGenerationOptions options)
        {
            string appName = options.AppName;
            string spiderlyVersion = options.SpiderlyVersion;
            DbProviderCodes dbProvider = options.DbProvider;
            PackageManagerCodes packageManager = options.PackageManager;

            return new SpiderlyFolder
            {
                Name = appName.ToKebabCase(),
                ChildFolders =
                {
                    new SpiderlyFolder
                    {
                        Name = ".vscode",
                        Files =
                        {
                            new SpiderlyFile { Name = "extensions.json", Data = GetExtensionsJsonData(dbProvider) },
                            new SpiderlyFile { Name = "launch.json", Data = GetLaunchJsonData(appName, packageManager) },
                            new SpiderlyFile { Name = "settings.json", Data = GetSettingsJsonData() },
                            new SpiderlyFile { Name = "tasks.json", Data = GetTasksJsonData(appName) },
                        }
                    },
                    FrontendTemplate.Folder(options),
                    BackendTemplate.Folder(options),
                },
                Files =
                {
                    new SpiderlyFile { Name = ".gitignore", Data = GetGitIgnoreData() },
                    new SpiderlyFile { Name = "README.md", Data = GetREADMEData(appName, spiderlyVersion) },
                    new SpiderlyFile { Name = "CLAUDE.md", Data = GetClaudeMdData(appName) },
                }
            };
        }

        private static string GetExtensionsJsonData(DbProviderCodes dbProvider)
        {
            string dbExtension = dbProvider == DbProviderCodes.PostgreSQL
                ? "ckolkman.vscode-postgres"
                : "ms-mssql.mssql";

            return $$"""
{
  "recommendations": [
    "angular.ng-template",
    "formulahendry.auto-rename-tag",
    "{{dbExtension}}",
    "esbenp.prettier-vscode"
  ]
}

""";
        }

        private static string GetLaunchJsonData(string appName, PackageManagerCodes packageManager)
        {
            string pmCommand = packageManager.GetCommandName();

            return $$"""
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": "Launch Backend (.NET)",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build",
      "program": "dotnet",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/Backend/{{appName}}.WebAPI/{{appName}}.WebAPI.csproj"
      ],
      "cwd": "${workspaceFolder}/Backend/{{appName}}.WebAPI",
      "stopAtEntry": false,
      "launchSettingsProfile": "http",
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    },
    {
      "name": "Launch Frontend (Angular)",
      "type": "node",
      "request": "launch",
      "runtimeExecutable": "{{pmCommand}}",
      "runtimeArgs": ["start"],
      "cwd": "${workspaceFolder}/Frontend"
    }
  ],
  "compounds": [
    {
      "name": "Launch (Backend + Frontend)",
      "configurations": ["Launch Backend (.NET)", "Launch Frontend (Angular)"],
      "stopAll": true,
      "presentation": {
        "hidden": false,
        "group": "{{appName}}",
        "order": 1
      }
    }
  ]
}

""";
        }

        private static string GetSettingsJsonData()
        {
            return """
{
  "editor.defaultFormatter": "esbenp.prettier-vscode",
  "editor.formatOnSave": true,
  "editor.codeActionsOnSave": {
    "source.fixAll": "explicit",
    "source.organizeImports": "explicit"
  },

  "[csharp]": {
    "editor.defaultFormatter": "ms-dotnettools.csharp"
  }
}
""";
        }

        private static string GetTasksJsonData(string appName)
        {
            return $$"""
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build",
      "type": "shell",
      "command": "dotnet",
      "args": [
        "build",
        "Backend/{{appName}}.WebAPI/{{appName}}.WebAPI.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "options": {
        "cwd": "${workspaceFolder}"
      },
      "problemMatcher": "$msCompile",
      "group": {
        "kind": "build",
        "isDefault": true
      }
    }
  ]
}

""";
        }

        private static string GetGitIgnoreData()
        {
            return $$"""
# C#
**/.vs/
**/*.exe
**/*.dll
**/*.log
**/bin/
**/obj/
**/*.user
**/*.suo
**/*.pdb
**/FileStorage

# Angular
**/dist/
**/tmp/
**/out-tsc/
**/bazel-out/
**/.angular/cache/

# Node
**/node_modules/
**/npm-debug.log
**/yarn-error.log
**/pnpm-debug.log
**/.pnpm-debug.log
**/*.env
**/*.env.local

# Local dev overrides (real secrets stay out of git)
**/appsettings.Development.local.json
**/appsettings.*.local.json

# IDEs and editors
**/.idea/
**/.project
**/.classpath
**/.c9/
**/*.launch
**/.settings/
**/*.sublime-workspace

# Visual Studio Code
.vscode/*
!.vscode/settings.json
!.vscode/tasks.json
!.vscode/launch.json
!.vscode/extensions.json
**/.history/*

# Miscellaneous
**/.sass-cache/
**/connect.lock
**/coverage
**/libpeerconnection.log
**/testem.log
**/typings
**/*.pid
**/*.bak
**/*.tmp

# Spiderly agent guidance — machine-local junctions regenerated by `spiderly agent-sync`
# (AGENTS.md is committed; these links point at absolute node_modules paths, so they're per-machine)
**/.claude/skills/spiderly-*

# Spiderly machine-local config (e.g. agent-sync workspace target); committed config is .spiderly/config.json
**/.spiderly/*.local.json

# System files
**/.DS_Store
**/Thumbs.db
""";
        }

        private static string GetREADMEData(string appName, string spiderlyVersion)
        {
            return $$"""
# {{appName}}
This project was generated with [Spiderly CLI](https://github.com/filiptrivan/spiderly/tree/main/Spiderly.CLI) version {{spiderlyVersion}}.

For more information about Spiderly, visit our [documentation](https://www.spiderly.dev/docs/getting-started).
""";
        }

        private static string GetClaudeMdData(string appName)
        {
            return $$"""
# {{appName}}

A Spiderly application — .NET 9 backend + Angular 19 admin panel, scaffolded by Spiderly CLI.

## What is Spiderly

Spiderly is a code generator. You define EF Core entities as C# classes decorated with custom attributes; source generators emit DTOs, controllers, services, FluentValidation rules, Mapster mappers, Angular CRUD pages, TypeScript entity classes, validators, and translation entries.

**Hand-written code extends generated base classes.** Generated files are regenerated on every build — never edit them directly. Custom logic lives in entity service overrides, custom controllers, custom DTOs, and override hooks.

**The Angular files are generated by the .NET build, not by `ng`.** The generators are Roslyn source generators in the backend, but the Angular ones don't emit into the C# compilation — they write `.ts` files straight to disk in the sibling `Frontend/` project (entities, enums, validators, the API service, base detail pages, as `*.generated.ts`). So you regenerate the frontend by building the `Backend/` solution, not via the Angular CLI.

## Layout

- `Backend/` — .NET 9 solution
  - `{{appName}}.WebAPI` — ASP.NET host
  - `{{appName}}.Business` — entity services (`{Entity}Service : {Entity}ServiceGenerated`), hand-written DTOs, business logic
  - `{{appName}}.Infrastructure` — `EntityModels/` (entity classes), DbContext, EF migrations
  - `{{appName}}.Migrations` — lightweight startup project for `dotnet ef` (avoids DLL locking when the WebAPI is running)
- `Frontend/` — Angular 19 SPA (admin panel)
  - `src/app/business/entities` — generated TypeScript entity classes
  - `src/app/business/components` — generated and custom CRUD pages
  - `src/app/business/services` — API service, auth service, layout service
- `tests/e2e/` — Playwright end-to-end tests

## Key conventions

- **Classification attributes are required** on hand-written classes (source generators enroll by attribute, not namespace):
  - Entities → `[SpiderlyEntity]`
  - M2M junctions → `[M2M]` **and** `[SpiderlyEntity]`
  - Custom controllers → `[SpiderlyController]`
  - Hand-written DTOs → `[SpiderlyDTO]`
  - Entity services extending the generated base → `[SpiderlyService]`
  - Hand-written partial mapper class → `[SpiderlyDataMapper]`
  - C# enums / class-based string-constant enums exposed to Angular → `[SpiderlyEnum]`
- **Database table names are singular** — match the entity class name exactly (`Category` class → `"Category"` table).
- **`bool?` is preferred** over non-nullable `bool` for checkbox properties; treat `null` as `false`.
- **EF migrations**: run `dotnet ef` commands from `Backend/{{appName}}.Migrations/` as the startup project.

## Working with Claude Code

Spiderly's AI-agent guidance is **version-matched to your installed package**. The `spiderly` npm package ships docs + skills under `Frontend/node_modules/spiderly/agent/`, and `spiderly agent-sync` (run automatically by `spiderly init`) projects them into this project:

- **`AGENTS.md`** — an always-on pointer telling agents to read version-matched Spiderly reference docs from `Frontend/node_modules/spiderly/agent/docs/`. `CLAUDE.md` imports it via `@AGENTS.md`, so it's always in context. Cross-agent (Cursor, Copilot, Codex too).
- **`.claude/skills/spiderly-*`** — on-demand, trigger-based skills for deeper workflows (scaffold an entity, EF migrations, deployment, upgrade). Run `/skills` to list.

Re-run **`spiderly agent-sync`** anytime to refresh both after upgrading the package — it's idempotent and reconciles renamed/removed skills automatically.
""";
        }
    }
}
