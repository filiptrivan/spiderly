import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './agnostic.mjs';

test('agnostic provision copies the fixture and writes an AGENTS.md index of doc surfaces only', () => {
  // Fake bundle with one doc + one skill surface.
  const bundle = mkdtempSync(join(tmpdir(), 'bundle-'));
  writeFileSync(join(bundle, 'manifest.json'), JSON.stringify({
    skills: [
      { name: 'entity-design', surface: 'doc', description: 'Design entities.' },
      { name: 'deployment', surface: 'skill', description: 'Deploy the app.' },
    ],
  }));
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  // task.fixture 'trivial' resolves under the harness fixturesRoot (exists from Task 1).
  provision({ fixture: 'trivial' }, ws, { bundlePath: bundle });

  assert.ok(existsSync(join(ws, 'README.md')), 'fixture file copied');
  const agents = readFileSync(join(ws, 'AGENTS.md'), 'utf8');
  assert.match(agents, /entity-design/, 'doc surface listed');
  assert.doesNotMatch(agents, /deployment/, 'skill surface must NOT be in the agnostic index');
});
