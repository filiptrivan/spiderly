import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './native.mjs';

// Build a fake bundle mirroring the real layout: skill-surface-only manifest + docs/ + skills/.
function fakeBundle() {
  const bundle = mkdtempSync(join(tmpdir(), 'bundle-'));
  mkdirSync(join(bundle, 'docs', 'entity-design'), { recursive: true });
  writeFileSync(join(bundle, 'docs', 'entity-design', 'index.md'), '# entity design');
  mkdirSync(join(bundle, 'skills', 'add-entity'), { recursive: true });
  writeFileSync(join(bundle, 'skills', 'add-entity', 'SKILL.md'), '# add entity');
  writeFileSync(join(bundle, 'manifest.json'), JSON.stringify({
    skills: [{ name: 'add-entity', surface: 'skill', description: 'Scaffold an entity.' }],
  }));
  return bundle;
}

test('native provision reproduces agent-sync: docs pointer, junctioned skills, CLAUDE import', () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  provision({ fixture: 'trivial' }, ws, { bundlePath: fakeBundle() });

  assert.ok(existsSync(join(ws, 'README.md')), 'fixture file copied');

  const agents = readFileSync(join(ws, 'AGENTS.md'), 'utf8');
  assert.match(agents, /<!-- BEGIN:spiderly -->/, 'agent-sync marker block present');
  assert.match(agents, /training data for Spiderly is stale/, 'verbatim pointer text');
  assert.match(agents, /\.spiderly\/agent\/docs\//, 'points at the staged docs dir');

  assert.ok(existsSync(join(ws, '.spiderly', 'agent', 'docs', 'entity-design', 'index.md')), 'docs staged');
  assert.ok(existsSync(join(ws, '.claude', 'skills', 'spiderly-add-entity', 'SKILL.md')), 'skill copied with spiderly- prefix');

  const claude = readFileSync(join(ws, 'CLAUDE.md'), 'utf8');
  assert.match(claude, /@AGENTS\.md/, 'CLAUDE.md imports AGENTS.md');
});

test('native provision without a bundle falls back to fixture-only (no throw)', () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  const emptyBundle = mkdtempSync(join(tmpdir(), 'nobundle-'));
  provision({ fixture: 'trivial' }, ws, { bundlePath: emptyBundle }); // no manifest.json
  assert.ok(existsSync(join(ws, 'README.md')), 'fixture still copied');
  assert.equal(existsSync(join(ws, 'AGENTS.md')), false, 'no AGENTS.md without a bundle');
});
