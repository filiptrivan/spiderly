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

function die(msg) {
  console.error(`agent-evals: ${msg}`);
  process.exit(1);
}

function parseArgs(argv) {
  if (argv[0] === 'run') argv = argv.slice(1); // optional `run` subcommand
  const a = { agents: ['claude'], track: 'agnostic', tier: undefined, reps: 3 };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i].startsWith('--') ? argv[i].slice(2) : null;
    if (!k) die(`unexpected argument: ${argv[i]}`);
    const v = argv[i + 1];
    if (v === undefined || v.startsWith('--')) die(`--${k} needs a value`);
    if (k === 'agents') { a.agents = v.split(',').filter(Boolean); i++; }
    else if (k === 'track') { a.track = v; i++; }
    else if (k === 'tier') { a.tier = v; i++; }
    else if (k === 'reps') { a.reps = Number(v); i++; }
    else die(`unknown flag: --${k}`);
  }
  if (!a.agents.length) die('--agents needs at least one agent');
  if (!Number.isInteger(a.reps) || a.reps < 1) die('--reps must be a positive integer');
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
