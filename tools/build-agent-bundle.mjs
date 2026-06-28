#!/usr/bin/env node
// Builds the agent guidance bundle shipped inside the `spiderly` npm package.
//
// Authoring source — TWO trees, where location IS the doc/skill taxonomy (no side-car map):
//   claude-plugins/docs/<name>/index.md     — reference docs (browsed via the AGENTS.md pointer)
//   claude-plugins/skills/<name>/SKILL.md   — workflow skills (junctioned into .claude/skills)
//
// Output (committed build artifact, like the framework-metadata SSOT):
//   Angular/projects/spiderly/agent/manifest.json   — machine-readable contract (skill-surface only)
//   Angular/projects/spiderly/agent/docs/**          — reference docs (browsed via AGENTS.md pointer)
//   Angular/projects/spiderly/agent/skills/**        — workflow skills (junctioned into .claude/skills)
//
// ng-packagr copies agent/** into dist/spiderly/agent during `ng build spiderly`, so it lands
// at node_modules/spiderly/agent in consumer apps — version-pinned. `Spiderly.CLI agent-sync`
// reads manifest.json from there and projects it into the consumer (AGENTS.md + .claude/skills).
//
// DO NOT hand-edit anything under agent/ — change the source + re-run this. CI/pre-commit
// regenerate and fail on any git diff, so a stale bundle can never merge.
//
//   node tools/build-agent-bundle.mjs
//
// Zero dependencies.

import { readFileSync, writeFileSync, readdirSync, existsSync, rmSync, mkdirSync, cpSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');
const bundleRoot = join(repoRoot, 'Angular', 'projects', 'spiderly', 'agent');

// The two authoring trees. `surface` is implied by location — no separate categorization file:
//   doc   → browsed via the always-on AGENTS.md pointer at agent/docs/
//   skill → junctioned into .claude/skills/spiderly-*
// The entry filename differs because Claude Code only recognizes a skill by its `SKILL.md`,
// while docs are plain reference and use `index.md`.
const TREES = [
  { surface: 'doc', root: join(repoRoot, 'claude-plugins', 'docs'), file: 'index.md', dest: 'docs' },
  { surface: 'skill', root: join(repoRoot, 'claude-plugins', 'skills'), file: 'SKILL.md', dest: 'skills' },
];

function fail(msg, details = []) {
  console.error(`ERROR: build-agent-bundle: ${msg}`);
  for (const d of details) console.error(`  - ${d}`);
  process.exit(1);
}

for (const t of TREES) if (!existsSync(t.root)) fail(`authoring tree not found at ${t.root}`);

// Parse `name` and `description` from a YAML frontmatter block.
function parseFrontmatter(filePath) {
  const text = readFileSync(filePath, 'utf8');
  const m = text.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!m) return {};
  const fields = {};
  for (const line of m[1].split(/\r?\n/)) {
    const idx = line.indexOf(':');
    if (idx === -1) continue;
    fields[line.slice(0, idx).trim()] = line.slice(idx + 1).trim();
  }
  return fields;
}

// --- Discover + validate everything up front, aggregate all problems, then fail once -------------
const errors = [];
const entries = [];

for (const t of TREES) {
  const allDirs = readdirSync(t.root, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => d.name)
    .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0)); // ordinal, deterministic

  for (const folder of allDirs) {
    // A folder without the expected entry file is a mis-placed or half-renamed entry
    // (e.g. a doc left in skills/, or a SKILL.md not yet renamed to index.md).
    if (!existsSync(join(t.root, folder, t.file))) {
      errors.push(`${t.dest}/${folder}: missing ${t.file}`);
      continue;
    }

    const fm = parseFrontmatter(join(t.root, folder, t.file));
    if (!fm.name) errors.push(`${t.dest}/${folder}/${t.file}: missing 'name' in frontmatter`);
    else if (fm.name !== folder) errors.push(`${t.dest}/${folder}/${t.file}: frontmatter name '${fm.name}' != folder name '${folder}'`);
    if (!fm.description) errors.push(`${t.dest}/${folder}/${t.file}: missing 'description' in frontmatter`);

    if (fm.name === folder && fm.description)
      entries.push({ name: folder, surface: t.surface, description: fm.description });
  }
}

if (errors.length) fail(`bundle validation failed (${errors.length})`, errors);

// --- Write the bundle (clean rebuild so renames/deletes propagate) ------------------------------
rmSync(bundleRoot, { recursive: true, force: true });
mkdirSync(join(bundleRoot, 'docs'), { recursive: true });
mkdirSync(join(bundleRoot, 'skills'), { recursive: true });

// Each entry's whole folder rides along (index.md/SKILL.md + any references/ or scripts/ subdirs).
for (const t of TREES)
  for (const e of entries.filter((e) => e.surface === t.surface))
    cpSync(join(t.root, e.name), join(bundleRoot, t.dest, e.name), { recursive: true });

// Manifest lists ONLY skill-surface entries — the CLI junctions these by name and prunes the rest.
// Docs need no enumeration; they're found by browsing agent/docs/.
const manifest = { skills: entries.filter((e) => e.surface === 'skill') };
writeFileSync(join(bundleRoot, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');

const docCount = entries.length - manifest.skills.length;
console.log(`build-agent-bundle: wrote ${docCount} doc(s) to agent/docs, ${manifest.skills.length} skill(s) to agent/skills + manifest`);
