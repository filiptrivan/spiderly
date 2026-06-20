import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './plain-net.mjs';

test('plain-net provision copies the baseline source, skips build output, writes no AGENTS.md', () => {
  // Fake fixtures root with a `plain-app` containing a source file AND a bin/ build artifact.
  const root = mkdtempSync(join(tmpdir(), 'fix-'));
  mkdirSync(join(root, 'plain-app', 'Backend', 'bin', 'Debug'), { recursive: true });
  writeFileSync(join(root, 'plain-app', 'Backend', 'Program.cs'), '// baseline');
  writeFileSync(join(root, 'plain-app', 'Backend', 'bin', 'Debug', 'Backend.dll'), 'BINARY');

  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  provision({}, ws, { root });

  assert.ok(existsSync(join(ws, 'Backend', 'Program.cs')), 'baseline source copied');
  assert.equal(existsSync(join(ws, 'Backend', 'bin')), false, 'build output must NOT be provisioned');
  assert.equal(existsSync(join(ws, 'AGENTS.md')), false, 'plain arm gets no Spiderly guidance');
});
