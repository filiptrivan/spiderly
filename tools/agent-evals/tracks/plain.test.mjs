import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './plain.mjs';

test('plain provision copies the fixture but writes NO Spiderly guidance', () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  // 'trivial' fixture exists under the harness fixturesRoot (used by the agnostic test too).
  provision({ fixture: 'trivial' }, ws);
  assert.ok(existsSync(join(ws, 'README.md')), 'fixture file copied');
  // The defining invariant of the plain track: it must add nothing Spiderly-aware.
  assert.equal(existsSync(join(ws, 'AGENTS.md')), false, 'plain track must not write an AGENTS.md');
});

test('plain provision tolerates an absent fixture (bare freestyle start)', () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  provision({ fixture: 'does-not-exist' }, ws); // must not throw
  assert.equal(existsSync(join(ws, 'AGENTS.md')), false);
});
