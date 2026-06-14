import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadTasks } from './tasks-loader.mjs';
import { runEval } from './run.mjs';
import noop from './agents/noop.mjs';
import oracle from './agents/oracle.mjs';
import { provision as agnostic } from './tracks/agnostic.mjs';

test('GATE: oracle scores 100% and no-op scores 0% on the trivial task', async () => {
  const tasks = loadTasks({ tier: 'atomic' }).filter((t) => t.id === 'trivial-marker');
  assert.equal(tasks.length, 1);

  const m = await runEval({
    agents: ['oracle', 'noop'],
    agentsByName: { oracle, noop },
    tracksByName: { agnostic },
    track: 'agnostic',
    tasks,
    reps: 2,
    runId: 'selftest',
  });

  const oracleRows = m.rows.filter((r) => r.agent === 'oracle');
  const noopRows = m.rows.filter((r) => r.agent === 'noop');
  assert.ok(oracleRows.length === 2 && oracleRows.every((r) => r.pass === true), 'oracle must pass all reps');
  assert.ok(noopRows.length === 2 && noopRows.every((r) => r.pass === false), 'no-op must fail all reps');
});
