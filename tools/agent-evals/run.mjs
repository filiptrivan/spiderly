import { mkdirSync } from 'node:fs';
import { join } from 'node:path';
import { resultsRoot } from './lib/paths.mjs';
import { provision as agnostic } from './tracks/agnostic.mjs';
import { runVerify } from './verify.mjs';
import { scoreRow } from './score.mjs';

const TRACKS = { agnostic };

// Run the full (agent × track × task × rep) grid. Each cell gets an isolated workspace.
// A thrown error is an INFRA failure (pass:null) — never counted as an agent green/red.
export async function runEval({ agents, agentsByName, track, tasks, reps, runId, meta = {} }) {
  const provisionFn = TRACKS[track];
  if (!provisionFn) throw new Error(`unknown track: ${track}`);

  const rows = [];
  for (const agentName of agents) {
    const adapter = agentsByName[agentName];
    if (!adapter) throw new Error(`unknown agent: ${agentName}`);
    for (const task of tasks) {
      for (let rep = 0; rep < reps; rep++) {
        const ws = join(resultsRoot, runId, 'ws', `${agentName}-${track}-${task.id}-${rep}`);
        const row = { agent: agentName, track, taskId: task.id, rep, workspaceDir: ws };
        try {
          mkdirSync(ws, { recursive: true });
          provisionFn(task, ws);
          const ar = await adapter.run({
            task, workspaceDir: ws, prompt: task.promptText, maxTurns: task.maxTurns,
          });
          row.agentMeta = {
            tokens: ar.tokens, costUsd: ar.costUsd, wallMs: ar.wallMs,
            turns: ar.turns, cleanExit: ar.cleanExit,
          };
          const vr = await runVerify(task, ws);
          row.checks = vr.checks;
          row.pass = scoreRow(task, vr).pass;
          row.error = null;
        } catch (err) {
          row.pass = null;
          row.error = String(err?.message ?? err);
        }
        rows.push(row);
      }
    }
  }
  return { meta: { runId, track, reps, ...meta }, rows };
}
