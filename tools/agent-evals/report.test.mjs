import { test } from 'node:test';
import assert from 'node:assert/strict';
import { renderReport } from './report.mjs';

const matrix = {
  meta: { runId: 'r1', track: 'agnostic', reps: 2 },
  rows: [
    { agent: 'claude', taskId: 'add-validator', rep: 0, pass: true, checks: [{ name: 'compiles', pass: true }] },
    { agent: 'claude', taskId: 'add-validator', rep: 1, pass: false, checks: [{ name: 'compiles', pass: false }] },
    { agent: 'claude', taskId: 'add-validator', rep: 0, pass: null, error: 'boom', checks: [] },
  ],
};

test('renderReport shows pass rate, infra-error count, and a failure digest', () => {
  const md = renderReport(matrix);
  assert.match(md, /1\/2/, 'pass rate excludes the infra-error row');
  assert.match(md, /Failure digest/);
  assert.match(md, /compiles/, 'failed check named in the digest');
});
