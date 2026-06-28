# Agent guidance distribution — Design

> Status: **DONE.** Tracking: [filiptrivan/spiderly#250](https://github.com/filiptrivan/spiderly/issues/250).
> Decisions locked: source of truth = the versioned `spiderly` npm package; bundle wiring = **committed + guarded** (SSOT-style); projection = a new `Spiderly.CLI agent-sync` command that **reconciles** (see below).
> This doc proposes *where* Spiderly's agent guidance (skills + docs) should live and *how* it reaches a consumer app. It deliberately builds on the existing SSOT pipeline (`docs/framework-metadata-ssot.md`) rather than replacing it — that pipeline already solves "don't hand-duplicate reference *tables*"; this doc solves "ship the guidance version-matched and make agents actually use it."

## Problem

Spiderly ships agent guidance today as a **Claude Code plugin served from a GitHub marketplace** (`.claude-plugin/marketplace.json` → `./claude-plugins`). Two things are wrong with that channel:

1. **Flaky loading.** Installing/refreshing the plugin from GitHub (`~/.claude/plugins/cache/spiderly/...`) is unreliable.
2. **Version drift (the deeper problem).** The marketplace/plugin tracks a floating ref, not the Spiderly version the consumer actually installed. Spiderly is a **code generator** — its guidance is tightly coupled to the generated code shape (attributes, base classes, hook signatures), which changes between releases. v19.8 guidance fed to a v19.4 app is *worse than nothing*: it tells the agent to call APIs that don't exist yet.
3. **Claude-Code-only.** Plugins don't reach Cursor / Copilot / Codex.

## The two forces

Two — and only two — forces determine the right design.

### Force 1 — Trigger reliability → decides docs vs. skills

The core issue: a skill requires the agent to *decide* "should I look this up?" — and an agent doesn't know what it doesn't know. Vercel's published eval ([eval writeup](https://vercel.com/blog/agents-md-outperforms-skills-in-our-agent-evals)) quantifies this for their context, but the **underlying mechanism** is the deciding factor here, not the headline numbers: passive reference knowledge has no reliable trigger; explicit user-invoked workflows do.

- **Passive knowledge** ("how filtering works", "what attributes exist") has **no reliable trigger** → must be always-on context.
- **A workflow the user explicitly asks for** ("scaffold an entity", "upgrade Spiderly") **is** its own trigger → a skill works.

The docs-surface choice is further reinforced by **cross-agent reach** (Cursor/Copilot/Codex have no skill registry but do read `AGENTS.md`) and by Force 2 below. Vercel's inlined-prose evaluation pattern does not translate directly — our `AGENTS.md` block is a **pointer** to versioned files, not inlined content, so the surface choice rests on trigger reliability and version-pin, not on reproducing their specific eval setup.

### Force 2 — Version coupling → decides where it lives

The only mechanism that guarantees version-match with zero extra install/fetch is shipping guidance **inside the versioned artifact the consumer already depends on**. That is the real reason Next.js puts docs at `node_modules/next/dist/docs/` — not magic, just version-pinning ([next-16-2-ai](https://nextjs.org/blog/next-16-2-ai)). For a code generator this force is *stronger* than for a normal library.

## Design

### Source of truth: the versioned `spiderly` npm package

Ship both the bundled docs and the skills inside `Angular/projects/spiderly/` (published as the `spiderly` npm package). It already installs into the consumer's `Frontend/node_modules/spiderly/`, so guidance rides a dependency the consumer already pins → automatic version-match, no separate fetch.

Packaging detail to verify: the published tarball must actually include the new `docs/` + `skills/` folders (ng-packagr / `package.json#files` / `.npmignore`). A doc that isn't in the tarball is invisible in `node_modules`.

> ⚠️ **NuGet is the wrong vehicle.** NuGet packages unpack to the global `~/.nuget/packages` store and content files land unpredictably under `obj/`; there is no clean, readable in-repo path for an agent to read. npm's flat, readable `node_modules` layout is what makes the Next.js pattern work.

### Docs vs. skills split

**Rule: declares allowed-tools or runs a command ⇒ skill; pure reference ⇒ doc.**

| → **Bundled docs** (always-on, indexed in `AGENTS.md`) | → **Skill** (explicit user trigger, junctioned into `.claude/skills`) |
|---|---|
| entity-design, angular-customization (control/validator maps), filtering-patterns, mapper-customization, custom-endpoints, backend-hooks, authorization, file-storage, backend-localization, frontend-localization, backend-testing, e2e-testing | add-entity, ef-migrations, spiderly-upgrade, deployment, verify-ui, report-gap |

`backend-testing` and `e2e-testing` are pure reference (patterns, not runnable workflows) → docs. `backend-hooks` and `authorization` are reference-shaped but reached for mid-task → docs (no reliable trigger). `add-entity` runs the `spiderly add-new-entity` scaffold workflow → skill.

### Surfacing — two channels, one source

The bundle is **physically split** in the npm package: `agent/docs/**` holds the browsable reference files; `agent/skills/**` holds the skill files that are junctioned into the consumer's `.claude/skills`. This split makes the two surfaces mechanically distinct — docs are read in place, skills are linked.

- **Docs** → a **static directory pointer** written into the consumer's repo-root `AGENTS.md`, pointing at `Frontend/node_modules/spiderly/agent/docs/`, with `CLAUDE.md` including it via `@AGENTS.md`. The pointer is **static** (no file enumeration, no version stamp) — enumerating skills into the block would cache a point-in-time manifest into the consumer's tracked file, going stale on every upgrade until `agent-sync` reruns. A bare directory pointer avoids that; the agent browses the directory on demand. `AGENTS.md` is the cross-agent standard (Cursor/Copilot/Codex/Claude), so one file reaches every agent.
  - The Spiderly-managed region is **marker-delimited** (`<!-- BEGIN:spiderly … -->` / `<!-- END:spiderly -->`), so a refresh replaces only that block and the consumer's own instructions survive — same pattern Next.js uses.
- **Skills** → **junctioned**, not copied: a junction/symlink from `node_modules/spiderly/agent/skills/*` into the consumer's `.claude/skills/spiderly-*`. **Claude Code does not auto-discover skills inside `node_modules`** — it scans `~/.claude/skills`, project `.claude/skills`, and installed plugins only. The link step is mandatory; handle Windows junction + POSIX symlink.

### The manifest contract + reconcile semantics

The authoring source is two trees — `claude-plugins/docs/<name>/index.md` (reference docs) and `claude-plugins/skills/<name>/SKILL.md` (workflow skills), each with `name` + `description` frontmatter. Which tree a folder lives in *is* its surface (doc vs skill) — there is no separate categorization file. A build step (`tools/build-agent-bundle.mjs`) packages both trees into the npm package and emits a single machine-readable contract the CLI consumes:

```jsonc
// node_modules/spiderly/agent/manifest.json  (no version field — SSOT-stable across bumps;
// the CLI stamps the version from the package's own package.json at sync time)
{
  "skills": [
    { "name": "entity-design",  "surface": "doc",   "description": "Design Spiderly entities…" },
    { "name": "ef-migrations",  "surface": "skill", "description": "Create and apply EF Core migrations…" }
    // … one entry per skill, sorted by name
  ]
}
```

`surface` (`doc` | `skill`) is implied by which authoring tree a folder lives in — `claude-plugins/docs/` vs `claude-plugins/skills/` — so there is no separate categorization file. Docs ship in `agent/docs/**` (browsed via the pointer); skills ship in `agent/skills/**` (junctioned). `surface` decides how each is projected.

**`agent-sync` is a reconcile, not an append.** Every run makes the consumer project match the manifest:

- **`AGENTS.md`** — the entire marker-delimited block is **rewritten** from the manifest's `doc` entries. Renamed/removed docs vanish from the index automatically; nothing stale survives.
- **`.claude/skills/`** — the desired set is `spiderly-<name>` for every `skill` entry. The command **adds missing, refreshes existing, and prunes any `spiderly-*` junction not in the manifest.** The `spiderly-` prefix scopes pruning to Spiderly's own junctions, never the user's.

This is what makes rename/add/delete self-heal (issue #250 discussion). The one timing nuance: because the bundle is version-pinned in `node_modules`, projected artifacts only update **when `agent-sync` runs** — so `spiderly init` and `spiderly-upgrade` invoke it, and it is idempotent and safe to re-run (the next run prunes any junction left dangling by an upgrade).

**Guard.** A rename also ripples to in-repo references that aren't auto-generated (`tools/gen-skill-docs.mjs` placement map, `framework-metadata-ssot.md`, cross-links, the website). The committed-bundle drift guard (CI + `.githooks/pre-commit`) regenerates the bundle and fails on diff, and additionally asserts *folder name == frontmatter `name`* and *each folder carries its `index.md` (docs) or `SKILL.md` (skills)* — catching a half-done rename at commit time.

### Projector: `Spiderly.CLI`

`Spiderly.CLI` (the NuGet global tool that runs `spiderly init`) becomes the projector. On `init` and on a new idempotent `agent-sync` command (also invoked by `spiderly-upgrade`):

1. Write/refresh the marker-delimited managed block in repo-root `AGENTS.md` (the compressed docs index for the installed version).
2. Ensure `CLAUDE.md` contains `@AGENTS.md`.
3. Create the skill junctions into `.claude/skills/spiderly-*`.
4. (Migration) once parity is reached, deprecate `.claude-plugin/marketplace.json` + `claude-plugins/`.

Must follow the `ai-agentic-design` rules: non-interactive by default, validate prerequisites upfront, fail loudly with non-zero exit.

### Workspace / umbrella target (`--agent-root`)

`agent-sync` resolves the **bundle source** and the **write target** independently. The bundle is read from the consumer project (the dir with `node_modules/spiderly/agent`), but guidance can be *projected* into a different root — for monorepos / umbrella workspaces where the AI agent runs from an outer dir that nests the Spiderly app, and Claude Code only scans the **outer** root's `.claude/skills` (not the nested project's).

- `--project-root <dir>` — where to read the bundle from (default: cwd).
- `--agent-root <dir>` — where to write `AGENTS.md`, the `CLAUDE.md` import, and `.claude/skills/spiderly-*` (default: the project root — i.e. unchanged behavior). Relative values resolve against the project root, so `..` targets the parent workspace. The docs pointer is computed relative to this target, so it correctly crosses into the nested app's `node_modules`; the junction targets are absolute, so they work regardless of target.
- `--save` — persist the chosen root to the consumer's machine-local `.spiderly/config.local.json` (`agentSync.root`). Later **bare** runs — including the one inside the `spiderly-upgrade` skill — reuse it without re-passing the flag.

Resolution order: `--agent-root` > `.spiderly/config.local.json` > `.spiderly/config.json` > project root. The persisted root is **machine-local** (the dev's umbrella layout), so it lives only in the gitignored `config.local.json`, never the committed `config.json` — otherwise it would impose one developer's directory shape on every other consumer of the same app repo.

### Relationship to the existing SSOT pipeline

`docs/framework-metadata-ssot.md` already derives reference **tables** (`*.generated.md`) from `framework-metadata.json` into `claude-plugins/docs/*/references/`. This design **keeps that pipeline** and changes only the *destination*:

- The generator (`tools/gen-skill-docs.mjs`) emits into the npm package's `docs/`/`skills/` tree instead of (or in addition to) `claude-plugins/`.
- Skill **bodies** (`SKILL.md`) are still hand-authored, but should live in **one** canonical place and be packaged from there — see open decision 1.
- `spiderly-website` continues to consume `framework-metadata.json` (release.yml already pushes it), so the website and the bundled docs render from the same SSOT.

### Version sync

The package version already lives in `Angular/projects/spiderly/package.json` and is bumped by `release.yml`. Because docs/skills ship *inside* that package, they version-bump for free — no separate `marketplace.json` version to keep in lockstep (that file goes away with the plugin).

## Considered and rejected

- **Keep the GitHub plugin marketplace** — the status quo. Floating-version, extra fetch, Claude-only. The thing we're fixing.
- **`npx skills add filiptrivan/spiderly` as the source.** Better *delivery* (cross-agent, robust, automates the link step) but `owner/repo` is a **floating ref** → reintroduces the exact version drift that hurts a code generator most. Acceptable only if pointed at a **version-pinned** package tarball, at which point it's just a transport over the npm-package source — keep it as an optional transport, not the source of truth.
- **All-docs (no skills at all).** Throws away the one place skills genuinely win — explicit triggered workflows (scaffold/upgrade/migrate) — and risks context bloat without the index. Rejected in its pure form.
- **All-skills (lean into skills everywhere).** Cargo-culting against the evidence; passive knowledge silently fails to trigger.

## Eval / validation strategy

Mirror what the framework author who invented this approach actually does — and *doesn't* do.

Vercel does **not** run agent evals as a blocking per-PR gate. Their `next-evals-oss` CI has a single workflow that runs `pnpm eval --dry` and asserts "Nothing to run" — a **cache-completeness gate**, no agents executed. Real evals run **out-of-band** (real API calls, money, non-deterministic); results are **committed**, then published to nextjs.org/evals. Their workflow even pins a canary SHA, noting that tracking bare `canary` *"makes this gate red for every PR whenever an eval changes upstream."*

For Spiderly, if/when we validate the split:

- Run agent evals **out-of-band** (manual or `workflow_dispatch`), commit results.
- At most, gate PRs on a cheap deterministic invariant (cache-completeness / "results committed").
- **Do not** wire live agent runs into a blocking per-PR gate.

This composes with the existing SSOT diff-guard model (cheap deterministic CI checks, expensive generation done out-of-band and committed).

## Open decisions

1. **Unify the doc source first.** Skill bodies live in `claude-plugins/skills/`; website prose lives in `spiderly-website`. Best practice is one canonical source generated/packaged into both; otherwise the new system bakes in drift. **Recommend: unify before shipping.**
2. **Anchor when the npm package isn't installed.** The npm package lands in `Frontend/node_modules` — but a consumer doing backend-only work in a fresh clone may not have run `npm install`. Options: (a) accept the dependency (every Spiderly app has the Angular admin); (b) have `Spiderly.CLI agent-sync` **vendor** a version-stamped copy into a repo dir (e.g. `.spiderly/agent/`) read from the pinned package, independent of `node_modules`. **Recommend: npm-package-primary, with CLI fallback that resolves the version-pinned package.**

## Phased rollout

1. **Phase 1 — docs. ✅ DONE.** Bundle (`tools/build-agent-bundle.mjs` → `Angular/projects/spiderly/agent/`, shipped via ng-packagr assets); `spiderly agent-sync` reconciles the `AGENTS.md` index + `@AGENTS.md`. Drift guard (CI + pre-commit) extended.
2. **Phase 2 — skills. ✅ DONE.** `agent-sync` creates/prunes `.claude/skills/spiderly-*` junctions (Windows `mklink /J`, POSIX symlink) reconciled against the manifest.
3. **Phase 3 — adopt + deprecate. ✅ DONE.** Wired `agent-sync` into `InitCommand` and the `spiderly-upgrade` skill; switched the scaffold (`NetAndAngularFilesGenerator` README + `.claude/settings.json`) off the `spiderly@spiderly` plugin auto-enable and onto `@AGENTS.md` + junctions. Removed `.claude-plugin/marketplace.json` + the plugin manifests; `release.yml` no longer bumps the marketplace version. NOTE: `claude-plugins/skills/` stays — it's the bundle's authoring source; only the *plugin packaging* was removed.

## References

- Next.js 16.2 AI improvements — https://nextjs.org/blog/next-16-2-ai
- AGENTS.md outperforms skills in our agent evals — https://vercel.com/blog/agents-md-outperforms-skills-in-our-agent-evals
- Vercel Agent Skills docs — https://vercel.com/docs/agent-resources/skills
- Eval suite — https://github.com/vercel/next-evals-oss · results https://nextjs.org/evals
- Existing SSOT pipeline — `docs/framework-metadata-ssot.md`
