---
name: spiderly-upgrade
description: Upgrade a Spiderly consumer app's package version (NuGet + npm) to a newer Spiderly release. Use when the user asks to upgrade Spiderly, bump the Spiderly version, jump to a newer Spiderly release, or migrate to a newer Spiderly package version. Do NOT use for EF Core schema migrations (see ef-migrations skill).
---

# Spiderly Upgrade

End-to-end version upgrade for apps consuming Spiderly via the NuGet packages (`Spiderly.Shared`, `Spiderly.Security`, `Spiderly.Infrastructure`, `Spiderly.SourceGenerators`) and the `spiderly` npm package. (`Spiderly.CLI` is a `dotnet tool`, not a project reference — out of scope.)

Invocation forms:

- `/spiderly-upgrade <version>` — explicit target (e.g. `21.2.0`)
- `/spiderly-upgrade latest` — fetch latest stable from NuGet
- `/spiderly-upgrade` — same as `latest`

Spiderly has no backward-compatibility commitment — every release can break public API. This skill closes that gap by reading what changed on GitHub, scanning the user's app for what actually affects them, asking once, then applying everything with build-verification and self-healing retries.

## Step 1 — Pre-flight

Follow Spiderly's fail-loudly convention: every refused-to-start condition exits non-zero with an actionable message. Cache the parsed csproj and `package.json` contents from this step — Step 6 reuses them.

1. **Locate the app.** Glob `**/*.csproj` and `**/package.json` from CWD (skip `node_modules`, `bin`, `obj`). Identify the Backend (csproj with `Spiderly.*` PackageReferences) and Frontend (package.json with `"spiderly"` dep). If neither is found, exit:

   > Not a Spiderly app: no `Spiderly.*` PackageReference or `spiderly` npm dep found from this directory.

2. **Reject local-dev consumers.** If any csproj has a `<ProjectReference Include="...\spiderly\...">` pointing at a sibling Spiderly checkout, exit:

   > This skill is for NuGet/npm consumers. You're using local ProjectReferences to a sibling `spiderly/` directory — upgrade by `git pull` in that directory instead.

3. **Require clean git state.** Run `git status --porcelain`. If non-empty, exit:

   > Working tree must be clean before upgrading. Commit or stash your changes first — the upgrade will make many edits, and a clean tree is your `git restore .` safety net.

4. **Detect current version.** Read all `Spiderly.*` `PackageReference` `Version=` values across every csproj, plus the `"spiderly"` value in `Frontend/package.json`. They should agree. If they drift, list the values and ask the user which to treat as "current".

5. **Multi-solution monorepo.** If multiple Backend dirs (each with their own Spiderly refs) exist, ask the user which one to upgrade. One app per invocation.

## Step 2 — Resolve target version

- If arg is `latest` or omitted: WebFetch `https://api.nuget.org/v3-flatcontainer/spiderly.shared/index.json`, parse the `versions` array, pick the highest stable (no `-` suffix), confirm with the user.
- Otherwise validate the arg as `X.Y.Z` (or `X.Y.Z-preview.N` if the user is explicitly opting into a preview — warn that preview release notes are usually thin).
- Reject downgrades: if target < current, exit `Downgrades are not supported. To downgrade, edit Spiderly.* PackageReference Version= attributes and the "spiderly" npm dep manually, then run dotnet restore and npm install.`
- Reject no-op: if target == current, exit `Already on {version}.`

## Step 3 — Fetch what changed

Fire these two WebFetch calls **in parallel** (same message, two tool calls):

1. **Diff between tags.** `https://api.github.com/repos/filiptrivan/spiderly/compare/v{current}...v{target}` (three-dot syntax). Response has `files[]` with `filename` and `patch`. Filter client-side to the user-facing paths below — GitHub's compare endpoint has no path-filter parameter. If `truncated: true`, warn the user the diff was capped and proceed with what was returned.

2. **All release notes in one call.** `https://api.github.com/repos/filiptrivan/spiderly/releases?per_page=100`. Returns every release. Filter in-memory to tags `v{X.Y.Z}` strictly above current and ≤ target. Concatenate `body` fields in ascending version order. (One call, not N — Spiderly's total release count fits in one page.)

On 403 rate-limited: ask the user to export `GH_TOKEN` (no scopes needed) and retry.

### User-facing file paths (filter the compare response client-side)

**Include** — changes here affect consumers:

- `Spiderly.Shared/**/*.cs` — public attributes, contracts, builders
- `Spiderly.Security/**/*.cs` — auth interfaces, attributes
- `Spiderly.Infrastructure/**/*.cs` — DI extensions, base services
- `Spiderly.SourceGenerators/**/*.cs` — generator output shape changes (may break consumer overrides)
- `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` — init template; drift here implies existing apps may need mirroring edits (new service registration, new file in scaffold)
- `Angular/projects/spiderly/src/lib/**/*.ts`
- `Angular/projects/spiderly/src/public-api.ts`

**Exclude** — `*.Tests/**`, `tests/**`, `Spiderly.CLI/**` (consumers don't reference CLI internals — flag CLI flag/command changes only via release notes), `Angular/node_modules/**`, `Angular/dist/**`, `.github/**`, `.claude-plugin/**`, `claude-plugins/**`, `*.csproj`, `package.json`, `*.md`.

## Step 4 — Impact scan and plan

Read the combined release notes + filtered diff in a single pass. Identify changes that likely affect a consumer:

- New / renamed / removed public types, methods, attributes in `Spiderly.Shared`, `Spiderly.Security`, `Spiderly.Infrastructure`
- Changes in source-generator output shape (consumers may have overrides that no longer compile)
- Changes in the init template — signals what NEW apps look like; existing apps may need to mirror (e.g. a new `services.AddTransient<X>()`)
- Changes in the Angular library's public surface (`public-api.ts` exports, component/service contracts)
- CLI command/flag changes (if the user has CI scripts invoking `spiderly`)

For each candidate change:

1. Describe in one line what changed.
2. Use Grep over the user's codebase to find real usages. Read context, don't just match symbol names blindly.
3. If no real usages exist, drop the item from the plan.

Plan format:

```
Spiderly upgrade: 19.5.0 → 21.2.0

Code edits (N items affect you):
  1. <change> — <N> usages in <files>
  2. ...

Manual steps after upgrade:
  - <step the agent can't do for you>

Version bumps: Spiderly.*  19.5.0 → 21.2.0  (4 csproj + Frontend/package.json[ + .claude/settings.json skills pin])

After approval: apply → bump → restore + install (parallel) → build. Up to 2 build-fix retries on failure.
```

## Step 5 — Single approval gate

Present the plan and ask the user explicitly. Don't proceed without a clear yes.

## Step 6 — Apply

In this order. A failure at any step is a hard stop until the recovery procedure in Step 7.

1. **Code edits.** Apply each plan item via Edit. Read each target file first.
2. **Version bumps.** Iterate the csproj list cached in Step 1: rewrite every `<PackageReference Include="Spiderly.X" Version="OLD" />` to the new version. Rewrite `"spiderly": "OLD"` in `Frontend/package.json`. Don't re-glob.
3. **Skills pin.** If `.claude/settings.json` has an `extraKnownMarketplaces.spiderly.source`, ensure its `ref` is `v{target}` — rewrite if present, **add if missing** (ref-less sources track the default branch instead of the pinned version). No settings file or no spiderly source → skip; don't fabricate one.
4. **Restore packages in parallel.** Same message, two Bash calls: `dotnet restore` in Backend and `npm install` in Frontend. They share no locks. If either fails, surface the actual error and exit.
5. **`dotnet build`** from the Backend dir. On failure, go to Step 7.

## Step 7 — Build-failure recovery (max 2 retries)

Loop up to 2 times — stop on first green build:

1. Scan output for the **first `SPIDERLY`-prefixed diagnostic** (these are the root cause; downstream `CS0246` errors about missing `*DTO` types are noise). Fall back to the first `CS` error only if no SPIDERLY diagnostic exists.
2. Read the offending file. Apply the most likely fix using the error message + the diff context from Step 3.
3. Re-run `dotnet build`.

After 2 failed attempts, stop:

```
Upgrade halted after 2 build-fix attempts.

Build error still present:
  <paste the diagnostic>

Attempted fixes:
  1. <what>
  2. <what>

The in-progress upgrade is in your working tree.
  - To roll back: git restore .
  - To continue manually: fix the error, run `dotnet build`, then commit.
```

## Step 8 — Done

Print summary:

- Versions bumped (from → to)
- Files edited (count + list)
- Manual steps remaining (re-print verbatim from the plan)

Do **not** auto-commit. The user reviews the diff and commits themselves.

## Rules

- **Never auto-revert.** Leave the in-progress state in the working tree so the user can see what was attempted; they roll back with `git restore .`
- **Never silently drop a candidate breaking change.** If the diff shows one but you can't determine impact, include it in the plan as "verify manually".
