import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import noop from './noop.mjs';
import oracle from './oracle.mjs';

test('noop agent makes no changes', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  await noop.run({ task: { id: 'trivial-marker' }, workspaceDir: ws });
  assert.equal(existsSync(join(ws, 'result.txt')), false);
});

test('oracle agent applies the trivial-marker patch', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  await oracle.run({ task: { id: 'trivial-marker' }, workspaceDir: ws });
  assert.equal(readFileSync(join(ws, 'result.txt'), 'utf8').trim(), 'DONE');
});
