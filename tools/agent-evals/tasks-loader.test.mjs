import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadTasks } from './tasks-loader.mjs';

test('loadTasks finds the trivial-marker atomic task with all fields', () => {
  const tasks = loadTasks({ tier: 'atomic' });
  const t = tasks.find((x) => x.id === 'trivial-marker');
  assert.ok(t, 'trivial-marker task should be discovered');
  assert.equal(t.fixture, 'trivial');
  assert.deepEqual(t.required, ['marker-present']);
  assert.match(t.promptText, /result\.txt/);
  assert.ok(t.dir.endsWith('trivial-marker'));
});
