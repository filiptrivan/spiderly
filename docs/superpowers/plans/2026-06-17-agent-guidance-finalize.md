# Agent Guidance Distribution — Finalize (Phase 3 + split refinements) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the migration of Spiderly's AI-agent guidance off the GitHub plugin marketplace and onto the version-pinned npm bundle, with a drift-free static `AGENTS.md` pointer, a clean docs/skills surface split, and the plugin packaging removed.

**Architecture:** The `spiderly` npm package already ships a generated bundle at `node_modules/spiderly/agent/`. We (1) split that bundle physically by surface — `agent/docs/**` (reference, browsed via an always-on `AGENTS.md` pointer) and `agent/skills/**` (workflows, junctioned into `.claude/skills/spiderly-*`); (2) make the `AGENTS.md` block a **static, content-free directory pointer** so it never drifts or churns the consumer's tracked file; (3) correct the surface classification and preserve native slash invocation for `add-entity`; (4) retire the `.claude-plugin` marketplace packaging.

**Tech Stack:** Node ESM build script (`tools/build-agent-bundle.mjs`), .NET 9 CLI (`Spiderly.CLI`, `AgentSyncCommand`), GitHub Actions (`release.yml`), Markdown skills (`claude-plugins/skills/**`).

## Global Constraints

- **Branch:** All commits land on `develop` (spiderly's working branch) or a feature branch off it — **never `main`**. `release.yml` rebases `develop` onto `main` at release time. Confirm `git branch --show-current` before committing.
- **The bundle is a committed build artifact.** After any change to `claude-plugins/skills/**`, `tools/agent-surface.json`, or `tools/build-agent-bundle.mjs`, run `node tools/build-agent-bundle.mjs` and stage `Angular/projects/spiderly/agent/` **in the same commit**. CI and `.githooks/pre-commit` regenerate it and fail on any diff/untracked change. If metadata sources also changed, run `tools/regen-metadata.sh` **before** the bundle build (it feeds the reference tables).
- **No version bumps in this work.** Versions are bumped only by `release.yml` at publish time. Do not hand-edit any `<Version>`, `package.json` version, or `marketplace.json` version.
- **No worthless unit tests.** This subsystem's safety net is the drift-guard (regen + diff), `dotnet build`, and a manual `agent-sync` smoke run — *not* xUnit tests. This overrides the writing-plans TDD default. Do not author CLI unit tests for the projector; verify via the commands each task specifies.
- **English only** for all identifiers and comments.
- **Do not start/kill** the backend or any dev server. `spiderly agent-sync` is a one-shot CLI call and is fine to run.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `tools/build-agent-bundle.mjs` | Generate the bundle from skills + surface map | Split output into `agent/docs/` + `agent/skills/`; manifest = skill-surface only |
| `tools/agent-surface.json` | Surface classification (doc/skill) | Flip `backend-testing`+`e2e-testing` → doc; add `add-entity` → skill; document the rule |
| `claude-plugins/skills/add-entity/SKILL.md` | The add-entity workflow as a skill | **Create** (moved from `commands/add-entity.md`) |
| `claude-plugins/commands/add-entity.md` | Old plugin slash command | **Delete** |
| `Spiderly.CLI/Commands/AgentSyncCommand.cs` | Project bundle into a consumer | Static `AGENTS.md` pointer → `agent/docs/`; junctions ← `agent/skills/` |
| `claude-plugins/skills/spiderly-upgrade/SKILL.md` | Upgrade workflow | Append an `agent-sync` refresh step |
| `.github/workflows/release.yml` | Release automation | Stop bumping `marketplace.json` |
| `.claude-plugin/marketplace.json`, `claude-plugins/plugin.json`, `claude-plugins/commands/` | Plugin packaging | **Delete** |
| `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs` | Consumer scaffold templates | De-enumerate the agent-guidance prose; drop "testing" from the skills list |
| `docs/agent-guidance-distribution.md`, `CLAUDE.md` | Design + contributor docs | Reflect the split, static pointer, framing, phase completion |

---

## Task 1: Split the bundle by surface

**Files:**
- Modify: `tools/build-agent-bundle.mjs:88-98`
- Modify: `CLAUDE.md` ("Agent guidance bundle" section — the `agent/` output description)
- Verify: `Angular/projects/spiderly/ng-package.json` (assets glob ships `agent/**`)

**Interfaces:**
- Produces: bundle layout `agent/docs/<name>/` (doc-surface), `agent/skills/<name>/` (skill-surface), and `agent/manifest.json` = `{ "skills": [ { name, surface, description } ] }` containing **only** skill-surface entries. Task 2 reads `agent/docs/` (pointer target) and `agent/skills/` + `manifest.json` (junction list).

- [ ] **Step 1: Replace the bundle-write block**

In `tools/build-agent-bundle.mjs`, replace lines 88-98 (from `// --- Write the bundle` through the final `console.log`) with:

```js
// --- Write the bundle (clean rebuild so renames/deletes propagate) ------------------------------
rmSync(bundleRoot, { recursive: true, force: true });
mkdirSync(join(bundleRoot, 'docs'), { recursive: true });
mkdirSync(join(bundleRoot, 'skills'), { recursive: true });

// Split by surface so each skill has exactly ONE discovery channel:
//   agent/docs/<name>   — reference, browsed via the always-on AGENTS.md pointer
//   agent/skills/<name> — workflow, junctioned into .claude/skills/spiderly-*
for (const s of skills) {
  const dest = s.surface === 'doc' ? 'docs' : 'skills';
  cpSync(join(skillsRoot, s.name), join(bundleRoot, dest, s.name), { recursive: true });
}

// Manifest lists ONLY skill-surface entries — the CLI junctions these by name and prunes the
// rest. Doc-surface skills need no enumeration; they're found by browsing agent/docs/.
const manifest = { skills: skills.filter((s) => s.surface === 'skill') };
writeFileSync(join(bundleRoot, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');

const docCount = skills.filter((s) => s.surface === 'doc').length;
console.log(`build-agent-bundle: wrote ${docCount} doc(s) to agent/docs, ${skills.length - docCount} skill(s) to agent/skills + manifest`);
```

- [ ] **Step 2: Update the header comment**

In the same file, update the `// Output (committed build artifact...)` block (lines ~7-9) to:

```js
// Output (committed build artifact, like the framework-metadata SSOT):
//   Angular/projects/spiderly/agent/manifest.json   — machine-readable contract (skill-surface only)
//   Angular/projects/spiderly/agent/docs/**          — reference docs (browsed via AGENTS.md pointer)
//   Angular/projects/spiderly/agent/skills/**        — workflow skills (junctioned into .claude/skills)
```

- [ ] **Step 3: Confirm ng-packagr ships the whole `agent/` tree**

Run: `grep -n "agent" Angular/projects/spiderly/ng-package.json`
Expected: an assets entry covering `agent` or `agent/**` (a directory copy — subfolders ride along automatically). If it lists `agent/skills` explicitly, broaden it to `agent` (or add `agent/docs`).

- [ ] **Step 4: Regenerate the bundle**

Run: `node tools/build-agent-bundle.mjs`
Expected: `wrote 10 doc(s) to agent/docs, 7 skill(s) to agent/skills + manifest` (counts before Task 3's reclassification).

- [ ] **Step 5: Verify the new layout**

Run: `ls Angular/projects/spiderly/agent && ls Angular/projects/spiderly/agent/docs && ls Angular/projects/spiderly/agent/skills`
Expected: top level = `docs  manifest.json  skills`; `docs/` holds the 10 reference skills; `skills/` holds the 7 workflow skills; no flat skill folders remain at `agent/` root.

- [ ] **Step 6: Update CLAUDE.md output description**

In `CLAUDE.md` → "Agent guidance bundle — regenerate after skill changes", change the artifact description from `agent/` (`manifest.json` + `skills/**`) to: `agent/` (`manifest.json` + `docs/**` + `skills/**`), where `docs/**` is browsed via the `AGENTS.md` pointer and `skills/**` is junctioned. Keep the rest of the section (regen command, drift-guard note) unchanged.

- [ ] **Step 7: Commit**

```bash
git add tools/build-agent-bundle.mjs CLAUDE.md Angular/projects/spiderly/agent
git commit -m "refactor(agent-bundle): split bundle into docs/ + skills/ by surface"
```

---

## Task 2: Make the projector emit a static pointer + junction from `skills/`

**Files:**
- Modify: `Spiderly.CLI/Commands/AgentSyncCommand.cs`

**Interfaces:**
- Consumes: bundle layout from Task 1 (`agent/docs/`, `agent/skills/`, manifest = skill-only).
- Produces: a static marker-delimited `AGENTS.md` block (identical for every version — no skill names, no version stamp) pointing at `agent/docs/`; `.claude/skills/spiderly-<name>` junctions into `agent/skills/<name>` for each manifest entry.

- [ ] **Step 1: Replace `BuildBlock` with a static pointer**

In `AgentSyncCommand.cs`, replace the entire `BuildBlock` method (lines ~139-152) with:

```csharp
private static string BuildBlock(string relDocs)
{
    return
        BeginMarker + "\n" +
        "# Spiderly\n" +
        "\n" +
        "Your training data for Spiderly is stale. Before writing any Spiderly code, browse\n" +
        $"`{relDocs}/` and read the `SKILL.md` for the topic you're working on — these docs are\n" +
        "version-matched to the installed Spiderly package.\n" +
        EndMarker;
}
```

- [ ] **Step 2: Rewire `Execute` to the split layout**

In `Execute`, replace the block that computes `skillsDir`/`relSkills`/`version`/`docs`/`skillLinks` and calls `WriteAgentsBlock` (lines ~85-98) with:

```csharp
string agentDir = Path.GetDirectoryName(manifestPath);
string docsDir = Path.Combine(agentDir, "docs");
string skillsDir = Path.Combine(agentDir, "skills");
string relDocs = Path.GetRelativePath(cwd, docsDir).Replace('\\', '/');
string version = ReadPackageVersion(agentDir);

// manifest.json now lists skill-surface entries only — every entry is junctioned.
List<SkillEntry> skillLinks = manifest.Skills;

int created, pruned;
try
{
    WriteAgentsBlock(cwd, BuildBlock(relDocs));
    EnsureClaudeImport(cwd);
    (created, pruned) = ReconcileSkillJunctions(cwd, skillsDir, skillLinks);
}
catch (Exception ex)
{
    ConsoleHelper.MarkupLineERROR($"Failed to write agent guidance: {ex.Message}");
    return 1;
}

ConsoleHelper.MarkupLineOK(
    $"Synced AGENTS.md docs pointer + {skillLinks.Count} skill junction(s)" +
    (pruned > 0 ? $" ({pruned} stale pruned)" : "") +
    $", and ensured CLAUDE.md imports it" + (version != null ? $" (v{version})." : "."));
return 0;
```

- [ ] **Step 3: Delete the now-unused `Surface` helper**

Remove the `Surface(Manifest, string)` method (lines ~208-212) — the manifest is single-surface now, so it has no caller. Leave `SkillEntry.Surface` on the type (harmless; manifest still carries it).

- [ ] **Step 4: Build the CLI**

Run: `dotnet build Spiderly.CLI`
Expected: `Build succeeded`, 0 errors. (If `ReadPackageVersion` or any removed-symbol reference errors, fix the dangling reference — there should be none.)

- [ ] **Step 5: Commit**

```bash
git add Spiderly.CLI/Commands/AgentSyncCommand.cs
git commit -m "feat(agent-sync): static docs pointer + junction from split skills dir"
```

---

## Task 3: Reclassify testing skills + convert `add-entity` to a skill

**Files:**
- Create: `claude-plugins/skills/add-entity/SKILL.md` (content moved verbatim from the command)
- Delete: `claude-plugins/commands/add-entity.md`
- Modify: `tools/agent-surface.json`

**Interfaces:**
- Consumes: bundle build from Task 1 (the validator requires every skill folder be surfaced and every surface key have a folder).
- Produces: 12 doc-surface + 6 skill-surface skills; `add-entity` discoverable as `.claude/skills/spiderly-add-entity` after sync.

- [ ] **Step 1: Move the add-entity command into a skill folder**

```bash
mkdir -p claude-plugins/skills/add-entity
git mv claude-plugins/commands/add-entity.md claude-plugins/skills/add-entity/SKILL.md
```

The file already has valid skill frontmatter (`name: add-entity`, `description: …`) and the folder name matches `name`, so no body edit is needed. Verify:

Run: `head -4 claude-plugins/skills/add-entity/SKILL.md`
Expected: frontmatter with `name: add-entity`.

- [ ] **Step 2: Remove the now-empty commands directory**

Run: `rmdir claude-plugins/commands 2>/dev/null; ls claude-plugins`
Expected: `commands/` is gone; `plugin.json` and `skills/` remain (those go in Task 6).

- [ ] **Step 3: Update the surface map**

In `tools/agent-surface.json`, set `backend-testing` and `e2e-testing` to `"doc"`, add `"add-entity": "skill"`, and update the `$comment` to record the rule. The `surfaces` object becomes:

```json
  "surfaces": {
    "angular-customization": "doc",
    "authorization": "doc",
    "backend-hooks": "doc",
    "backend-localization": "doc",
    "backend-testing": "doc",
    "custom-endpoints": "doc",
    "e2e-testing": "doc",
    "entity-design": "doc",
    "file-storage": "doc",
    "filtering-patterns": "doc",
    "frontend-localization": "doc",
    "mapper-customization": "doc",
    "add-entity": "skill",
    "deployment": "skill",
    "ef-migrations": "skill",
    "report-gap": "skill",
    "spiderly-upgrade": "skill",
    "verify-ui": "skill"
  }
```

Append to the `$comment` string: ` Rule: a skill that declares allowed-tools or runs a command is "skill"; pure reference knowledge is "doc".`

- [ ] **Step 4: Regenerate the bundle**

Run: `node tools/build-agent-bundle.mjs`
Expected: `wrote 12 doc(s) to agent/docs, 6 skill(s) to agent/skills + manifest`.

- [ ] **Step 5: Verify the moves landed**

Run: `ls Angular/projects/spiderly/agent/docs | grep -E "backend-testing|e2e-testing"; ls Angular/projects/spiderly/agent/skills | grep add-entity`
Expected: `backend-testing` and `e2e-testing` under `docs/`; `add-entity` under `skills/`.

- [ ] **Step 6: Commit**

```bash
git add claude-plugins/skills/add-entity tools/agent-surface.json Angular/projects/spiderly/agent
git rm claude-plugins/commands/add-entity.md
git commit -m "refactor(agent-surface): testing skills -> docs; add-entity command -> skill"
```

---

## Task 4: Wire `agent-sync` into the upgrade workflow

**Files:**
- Modify: `claude-plugins/skills/spiderly-upgrade/SKILL.md`

**Interfaces:**
- Consumes: nothing structural — documents that the upgrade flow refreshes projected guidance.

- [ ] **Step 1: Append a refresh step**

At the end of the upgrade procedure in `claude-plugins/skills/spiderly-upgrade/SKILL.md` (after the package-version bump + install steps), add:

```markdown
## Refresh AI-agent guidance

After the new `spiderly` package is installed, project the version-matched guidance:

```bash
spiderly agent-sync
```

This is idempotent and reconciling: it rewrites the static `AGENTS.md` docs pointer, ensures `CLAUDE.md` imports it (`@AGENTS.md`), and adds/refreshes/prunes `.claude/skills/spiderly-*` junctions so renamed or removed skills self-heal. Re-running it is always safe.
```

- [ ] **Step 2: Regenerate the bundle (skill content changed)**

Run: `node tools/build-agent-bundle.mjs`
Expected: clean run; `git status` shows the updated `spiderly-upgrade/SKILL.md` mirrored under `agent/skills/spiderly-upgrade/`.

- [ ] **Step 3: Commit**

```bash
git add claude-plugins/skills/spiderly-upgrade/SKILL.md Angular/projects/spiderly/agent
git commit -m "docs(spiderly-upgrade): run agent-sync to refresh projected guidance"
```

---

## Task 5: Smoke-test the projector against the real consumer (pa-cms)

**Files:** none (verification only).

**Interfaces:**
- Consumes: the built CLI (Task 2) and a consumer with the bundle installed at `pa-cms/Frontend/node_modules/spiderly/agent/`.

> **Note:** pa-cms's `node_modules/spiderly` is the *published* package, which won't contain the new split until a release. To smoke-test the *new* projector against the *new* layout now, point the run at the freshly built local bundle, or temporarily copy `Angular/projects/spiderly/agent/` over `pa-cms/Frontend/node_modules/spiderly/agent/`. State which you did in the commit/PR notes.

- [ ] **Step 1: Run agent-sync against pa-cms from source**

Run (cwd = repo root `…/PACMS`):
```bash
dotnet run --project spiderly/Spiderly.CLI -- agent-sync
```
…with the working directory set to `pa-cms/` (or pass the project root the CLI expects). Expected: `Synced AGENTS.md docs pointer + 6 skill junction(s) …`.

- [ ] **Step 2: Verify the AGENTS.md block is the static pointer**

Run: `sed -n '/BEGIN:spiderly/,/END:spiderly/p' pa-cms/AGENTS.md`
Expected: the `# Spiderly … browse \`Frontend/node_modules/spiderly/agent/docs/\` …` block — **no** per-skill names, **no** version number.

- [ ] **Step 3: Verify junctions exist for the 6 skills and nothing stale**

Run: `ls pa-cms/.claude/skills`
Expected: exactly `spiderly-add-entity  spiderly-deployment  spiderly-ef-migrations  spiderly-report-gap  spiderly-spiderly-upgrade  spiderly-verify-ui` (junctions). No `spiderly-backend-testing` / `spiderly-e2e-testing` (those are docs now — pruned if previously present).

- [ ] **Step 4: Re-run to confirm idempotency**

Run the Step 1 command again.
Expected: same success line, `(0 stale pruned)`, and `git diff pa-cms/AGENTS.md` shows **no change** — the static block is stable across runs.

---

## Task 6: Retire the plugin packaging

**Files:**
- Modify: `.github/workflows/release.yml:612-618`
- Delete: `.claude-plugin/marketplace.json`, `claude-plugins/plugin.json`
- Modify: `Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs:4396-4397`

**Interfaces:** none downstream — this removes the deprecated Claude-only channel after parity is verified (Task 5).

- [ ] **Step 1: Stop bumping marketplace.json in release.yml**

In `.github/workflows/release.yml`, delete the marketplace `sed` line and drop it from the `git add`, keeping the CLI bump. Replace lines 612-618 with:

```yaml
          # Bump spiderly-cli/package.json in lockstep with the framework.
          # (lives in finalize, not a build matrix lane — produces no artifact)
          new_version="${{ needs.validate.outputs.new_version }}"
          sed -i "s|\"version\": \"[^\"]*\"|\"version\": \"$new_version\"|" spiderly-cli/package.json
          git add spiderly-cli/package.json
```

- [ ] **Step 2: Delete the plugin manifests**

```bash
git rm .claude-plugin/marketplace.json claude-plugins/plugin.json
rmdir .claude-plugin 2>/dev/null || true
```

Expected: `claude-plugins/` now contains only `skills/` (the authoring source) — confirm with `ls claude-plugins`.

- [ ] **Step 3: De-enumerate the scaffold prose**

In `NetAndAngularFilesGenerator.cs`, replace the two bullet lines at 4396-4397 with:

```
- **`AGENTS.md`** — an always-on pointer telling agents to read version-matched Spiderly reference docs from `Frontend/node_modules/spiderly/agent/docs/`. `CLAUDE.md` imports it via `@AGENTS.md`, so it's always in context. Cross-agent (Cursor, Copilot, Codex too).
- **`.claude/skills/spiderly-*`** — on-demand, trigger-based skills for deeper workflows (scaffold an entity, EF migrations, deployment, upgrade). Run `/skills` to list.
```

(Drops the drift-prone topic enumeration and the now-incorrect "testing" workflow reference.)

- [ ] **Step 4: Confirm no lingering plugin references**

Run: `grep -rn "spiderly@spiderly\|marketplace\|claude-plugin" --include=*.cs --include=*.yml --include=*.json . | grep -v node_modules`
Expected: no matches in shipped scaffold/CI (matches only in `docs/` design history are fine and handled in Task 7).

- [ ] **Step 5: Build to confirm the scaffold string still compiles**

Run: `dotnet build Spiderly.Shared`
Expected: `Build succeeded`.

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/release.yml Spiderly.Shared/Helpers/NetAndAngularFilesGenerator.cs
git rm .claude-plugin/marketplace.json claude-plugins/plugin.json
git commit -m "chore(agent-guidance): remove GitHub plugin marketplace packaging"
```

---

## Task 7: Update design doc, CLAUDE.md, and issue #250

**Files:**
- Modify: `docs/agent-guidance-distribution.md`
- Modify: `CLAUDE.md` ("Versioning" — drop marketplace.json from the version-locations list)

- [ ] **Step 1: Mark the design doc done and record the decisions**

In `docs/agent-guidance-distribution.md`:
- Update the status line to `DONE` and Phase 3 to `✅ DONE`.
- In the "Docs vs. skills split" table, move `backend-testing` and `e2e-testing` into the docs column and add `add-entity` to the skills column. Add a one-line rule: *"declares allowed-tools / runs a command ⇒ skill; pure reference ⇒ doc."*
- In "Surfacing", note the bundle is physically split (`agent/docs/**` browsed, `agent/skills/**` junctioned) and the `AGENTS.md` block is a **static directory pointer** (no enumeration, no version stamp) — justified by avoiding a cached copy of the manifest in the consumer's tracked file.
- In "The two forces", soften the Vercel framing: the docs-surface choice is grounded on **version-pin (Force 2) + cross-agent reach** (Cursor/Copilot/Codex have no skill registry), not on reproducing Vercel's inlined-prose eval — our block is a pointer, not inlined content.

- [ ] **Step 2: Drop marketplace.json from CLAUDE.md version locations**

In `CLAUDE.md` → "Versioning", remove `and \`.claude-plugin/marketplace.json\` (\`plugins[0].version\`)` from the list of files that share the version.

- [ ] **Step 3: Commit**

```bash
git add docs/agent-guidance-distribution.md CLAUDE.md
git commit -m "docs(agent-guidance): record final split + plugin retirement"
```

- [ ] **Step 4: Close out issue #250**

Post a summary comment and close:
```bash
gh issue comment 250 --repo filiptrivan/spiderly --body "Implemented: bundle split into agent/docs (browsed via a static AGENTS.md pointer) + agent/skills (junctioned). Reclassified backend-testing/e2e-testing as docs; add-entity converted from a plugin command to a junctioned skill (keeps /-invocation). GitHub plugin marketplace packaging removed; release.yml no longer bumps marketplace.json. Design doc updated."
gh issue close 250 --repo filiptrivan/spiderly
```

---

## Task 8 (optional, local env): Clean up the dev workspace settings

**Files:**
- Modify: `…/PACMS/.claude/settings.json` (the workspace umbrella — **not** shipped)

> Do this only **after** Task 5 confirms junctions work in pa-cms, and only if you want the dev workspace off the plugin too. Removing the marketplace while the bundle isn't published yet means the spiderly consumer skills are available only where junctions exist (pa-cms), not when working inside the spiderly repo itself — which is fine, since you edit those skills at source there.

- [ ] **Step 1: Remove the plugin enable + marketplace**

In `…/PACMS/.claude/settings.json`, delete the `"spiderly@spiderly": true` entry from `enabledPlugins` and the `"spiderly"` entry from `extraKnownMarketplaces`. Save. (No commit needed if this file is gitignored in the umbrella; otherwise commit in the umbrella repo, not spiderly.)

---

## Self-Review

**Spec coverage** (decisions from the grilling session → task):
1. Static content-free `AGENTS.md` pointer → Task 2. ✅
2. Keep native slash invocation; split bundle to kill browse-vs-junction overlap → Task 1 (split) + Task 2 (junction from `skills/`). ✅
3. `add-entity` command → bundled skill → Task 3. ✅
4. Reclassify `backend-testing` + `e2e-testing` → doc → Task 3. ✅
5. Dual-run short/opt-in; Phase 3 removes the plugin → Task 5 (verify parity) gates Task 6 (remove). ✅
6. Re-ground framing off the Vercel "100%" → Task 7. ✅
7. Wire upgrade refresh → Task 4 (init already calls it at `InitCommand.cs:216`). ✅

**Placeholder scan:** No `TBD`/"add error handling"/"similar to" — `add-entity` is a verbatim `git mv` (content unchanged), and every code edit shows full replacement text. ✅

**Type consistency:** `BuildBlock(relDocs)` (Task 2) matches the `relDocs` computed in `Execute`; `manifest.Skills` is consumed as the junction list after `build-agent-bundle.mjs` writes skill-only entries (Task 1). `ReconcileSkillJunctions(cwd, skillsDir, skillLinks)` keeps its existing signature. ✅

**Ordering risk:** Task 1 must precede Task 2 (projector reads the split paths) and Task 3 (reclassified skills must land in the right split dir). Task 5 must precede Task 6 (don't remove the plugin until junctions are proven). Captured in task order.
