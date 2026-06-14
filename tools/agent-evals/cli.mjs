#!/usr/bin/env node
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { loadTasks } from './tasks-loader.mjs';
import { runEval } from './run.mjs';
import { renderReport } from './report.mjs';
import { resultsRoot } from './lib/paths.mjs';
import noop from './agents/noop.mjs';
import oracle from './agents/oracle.mjs';
import claude from './agents/claude.mjs';

const agentsByName = { noop, oracle, claude };

function parseArgs(argv) {
  if (argv[0] === 'run') argv = argv.slice(1); // optional `run` subcommand
  const a = { agents: ['claude'], track: 'agnostic', tier: undefined, reps: 3 };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i].startsWith('--') ? argv[i].slice(2) : null;
    const v = argv[i + 1];
    if (k === 'agents') { a.agents = v.split(','); i++; }
    else if (k === 'track') { a.track = v; i++; }
    else if (k === 'tier') { a.tier = v; i++; }
    else if (k === 'reps') { a.reps = Number(v); i++; }
  }
  return a;
}

const args = parseArgs(process.argv.slice(2));
const runId = new Date().toISOString().replace(/[:.]/g, '-');
const tasks = loadTasks({ tier: args.tier });
if (!tasks.length) { console.error('No tasks found'); process.exit(1); }

const matrix = await runEval({ ...args, agentsByName, tasks, runId });

mkdirSync(join(resultsRoot, runId), { recursive: true });
writeFileSync(join(resultsRoot, runId, 'matrix.json'), JSON.stringify(matrix, null, 2));
const report = renderReport(matrix);
writeFileSync(join(resultsRoot, runId, 'report.md'), report);
console.log(report);
