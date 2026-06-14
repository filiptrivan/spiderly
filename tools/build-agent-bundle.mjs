#!/usr/bin/env node
// Builds the agent guidance bundle shipped inside the `spiderly` npm package.
//
// Source of truth: claude-plugins/skills/<name>/ (SKILL.md frontmatter + references).
// Categorization:  tools/agent-surface.json (each skill -> "doc" | "skill").
//
// Output (committed build artifact, like the framework-metadata SSOT):
//   Angular/projects/spiderly/agent/manifest.json   — machine-readable contract the CLI reads
//   Angular/projects/spiderly/agent/skills/**        — copy of the skills tree
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
const skillsRoot = join(repoRoot, 'claude-plugins', 'skills');
const surfaceConfigPath = join(repoRoot, 'tools', 'agent-surface.json');
const bundleRoot = join(repoRoot, 'Angular', 'projects', 'spiderly', 'agent');

const VALID_SURFACES = ['doc', 'skill'];

function fail(msg, details = []) {
  console.error(`ERROR: build-agent-bundle: ${msg}`);
  for (const d of details) console.error(`  - ${d}`);
  process.exit(1);
}

if (!existsSync(skillsRoot)) fail(`skills directory not found at ${skillsRoot}`);
if (!existsSync(surfaceConfigPath)) fail(`surface config not found at ${surfaceConfigPath}`);

const surfaces = JSON.parse(readFileSync(surfaceConfigPath, 'utf8')).surfaces ?? {};

// Discover skill folders (a folder is a skill iff it has a SKILL.md).
const skillDirs = readdirSync(skillsRoot, { withFileTypes: true })
  .filter((d) => d.isDirectory() && existsSync(join(skillsRoot, d.name, 'SKILL.md')))
  .map((d) => d.name)
  .sort((a, b) => (a < b ? -1 : a > b ? 1 : 0)); // ordinal, deterministic

// Parse `name` and `description` from a SKILL.md YAML frontmatter block.
function parseFrontmatter(folder) {
  const text = readFileSync(join(skillsRoot, folder, 'SKILL.md'), 'utf8');
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

// --- Validate everything up front, aggregate all problems, then fail once -----------------------
const errors = [];
const skills = [];

for (const folder of skillDirs) {
  const fm = parseFrontmatter(folder);
  if (!fm.name) errors.push(`${folder}/SKILL.md: missing 'name' in frontmatter`);
  else if (fm.name !== folder) errors.push(`${folder}/SKILL.md: frontmatter name '${fm.name}' != folder name '${folder}'`);
  if (!fm.description) errors.push(`${folder}/SKILL.md: missing 'description' in frontmatter`);

  const surface = surfaces[folder];
  if (!surface) errors.push(`${folder}: not categorized in tools/agent-surface.json (add "doc" or "skill")`);
  else if (!VALID_SURFACES.includes(surface)) errors.push(`${folder}: invalid surface '${surface}' (must be "doc" or "skill")`);

  if (fm.name === folder && fm.description && VALID_SURFACES.includes(surface))
    skills.push({ name: folder, surface, description: fm.description });
}

// Surface entries with no matching skill folder (stale config).
for (const key of Object.keys(surfaces))
  if (!skillDirs.includes(key)) errors.push(`tools/agent-surface.json: '${key}' has no claude-plugins/skills/${key} folder`);

if (errors.length) fail(`bundle validation failed (${errors.length})`, errors);

// --- Write the bundle (clean rebuild so renames/deletes propagate) ------------------------------
rmSync(bundleRoot, { recursive: true, force: true });
mkdirSync(bundleRoot, { recursive: true });

cpSync(skillsRoot, join(bundleRoot, 'skills'), { recursive: true });

const manifest = { skills }; // no version field — kept SSOT-stable across release bumps
writeFileSync(join(bundleRoot, 'manifest.json'), JSON.stringify(manifest, null, 2) + '\n', 'utf8');

const docs = skills.filter((s) => s.surface === 'doc').length;
console.log(`build-agent-bundle: wrote manifest (${skills.length} skills: ${docs} doc, ${skills.length - docs} skill) + skills tree to agent/`);
