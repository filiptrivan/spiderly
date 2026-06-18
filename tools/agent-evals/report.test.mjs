import { test } from 'node:test';
import assert from 'node:assert/strict';
import { renderReport } from './report.mjs';

const matrix = {
  meta: { runId: 'r1', track: 'agnostic', reps: 2 },
  rows: [
    { agent: 'claude', taskId: 'add-validator', rep: 0, pass: true, checks: [{ name: 'compiles', pass: true }], agentMeta: { costUsd: 0.20, turns: 4, tokens: 100, wallMs: 1, cleanExit: true } },
    { agent: 'claude', taskId: 'add-validator', rep: 1, pass: false, checks: [{ name: 'compiles', pass: false }], agentMeta: { costUsd: 0.10, turns: 2, tokens: 50, wallMs: 1, cleanExit: true } },
    { agent: 'claude', taskId: 'add-validator', rep: 0, pass: null, error: 'boom', checks: [] },
  ],
};

test('renderReport shows pass rate, infra-error count, and a failure digest', () => {
  const md = renderReport(matrix);
  assert.match(md, /1\/2/, 'pass rate excludes the infra-error row');
  assert.match(md, /Failure digest/);
  assert.match(md, /compiles/, 'failed check named in the digest');
});

test('renderReport averages cost and turns over the reps that executed', () => {
  const md = renderReport(matrix);
  assert.match(md, /Mean cost/);
  assert.match(md, /Mean turns/);
  assert.match(md, /\$0\.1500/, 'mean cost = (0.20 + 0.10) / 2 over the two rows with agentMeta');
  assert.match(md, /3\.0/, 'mean turns = (4 + 2) / 2');
});
