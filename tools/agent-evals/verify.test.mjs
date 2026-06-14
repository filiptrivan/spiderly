import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { runVerify } from './verify.mjs';
import { loadTasks } from './tasks-loader.mjs';

const trivial = () => loadTasks({ tier: 'atomic' }).find((t) => t.id === 'trivial-marker');

test('runVerify passes the marker check when result.txt == DONE', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  writeFileSync(join(ws, 'result.txt'), 'DONE');
  const { checks } = await runVerify(trivial(), ws);
  assert.equal(checks[0].name, 'marker-present');
  assert.equal(checks[0].pass, true);
});

test('runVerify fails the marker check when the file is absent', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  const { checks } = await runVerify(trivial(), ws);
  assert.equal(checks[0].pass, false);
});
