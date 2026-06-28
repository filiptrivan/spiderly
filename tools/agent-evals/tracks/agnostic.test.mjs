import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './agnostic.mjs';

test('agnostic provision copies the bundled docs into the workspace and writes an AGENTS.md pointer', () => {
  // Realistic bundle shape (mirrors build-agent-bundle.mjs): doc-surface skills are FOLDERS under
  // docs/, and manifest.json lists ONLY skill-surface entries. So filtering docs out of the manifest
  // (the old bug) finds nothing — the docs must come from the docs/ folder, like agent-sync does.
  const bundle = mkdtempSync(join(tmpdir(), 'bundle-'));
  mkdirSync(join(bundle, 'docs', 'entity-design'), { recursive: true });
  writeFileSync(join(bundle, 'docs', 'entity-design', 'index.md'), '# Entity design\n');
  mkdirSync(join(bundle, 'skills', 'deployment'), { recursive: true });
  writeFileSync(join(bundle, 'manifest.json'), JSON.stringify({
    skills: [{ name: 'deployment', surface: 'skill', description: 'Deploy the app.' }],
  }));

  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  // task.fixture 'trivial' resolves under the harness fixturesRoot.
  provision({ fixture: 'trivial' }, ws, { bundlePath: bundle });

  assert.ok(existsSync(join(ws, 'README.md')), 'fixture file copied');
  // The doc reaches the workspace as a browsable file, and AGENTS.md points at docs/ (agent-sync style).
  assert.ok(existsSync(join(ws, 'docs', 'entity-design', 'index.md')), 'bundled doc copied into workspace');
  const agents = readFileSync(join(ws, 'AGENTS.md'), 'utf8');
  assert.match(agents, /docs\//, 'AGENTS.md points at the docs directory');
  // Skill-surface entries are NOT delivered by the docs-only agnostic track.
  assert.doesNotMatch(agents, /deployment/, 'skill surface must NOT be in the agnostic guidance');
});
