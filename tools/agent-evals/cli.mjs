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
import codex from './agents/codex.mjs';
import { provision as agnostic } from './tracks/agnostic.mjs';
import { provision as plainNet } from './tracks/plain-net.mjs';
import { provision as spiderly } from './tracks/spiderly.mjs';

const agentsByName = { noop, oracle, claude, codex };
// Framework axis (the case study): 'spiderly' = the WITH-Spiderly arm (a real, untouched
// `spiderly init` app — guidance included); 'plain-net' = the WITHOUT-Spiderly arm (frozen thin
// plain-.NET baseline). 'agnostic' is the older guidance-axis track (lean doc reconstruction), kept
// for that experiment. NOTE: the showcase has its OWN bare `plain` track in showcase.mjs —
// deliberately NOT wired into the scored benchmark here.
const tracksByName = { spiderly, agnostic, 'plain-net': plainNet };

function die(msg) {
  console.error(`agent-evals: ${msg}`);
  process.exit(1);
}

function parseArgs(argv) {
  if (argv[0] === 'run') argv = argv.slice(1); // optional `run` subcommand
  const a = { agents: ['claude'], track: 'agnostic', tier: undefined, task: undefined, reps: 3 };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i].startsWith('--') ? argv[i].slice(2) : null;
    if (!k) die(`unexpected argument: ${argv[i]}`);
    const v = argv[i + 1];
    if (v === undefined || v.startsWith('--')) die(`--${k} needs a value`);
    if (k === 'agents') { a.agents = v.split(',').filter(Boolean); i++; }
    else if (k === 'track') { a.track = v; i++; }
    else if (k === 'tier') { a.tier = v; i++; }
    else if (k === 'task') { a.task = v; i++; }
    else if (k === 'reps') { a.reps = Number(v); i++; }
    else die(`unknown flag: --${k}`);
  }
  if (!a.agents.length) die('--agents needs at least one agent');
  if (!Number.isInteger(a.reps) || a.reps < 1) die('--reps must be a positive integer');
  return a;
}

const args = parseArgs(process.argv.slice(2));
const runId = new Date().toISOString().replace(/[:.]/g, '-');
const tasks = loadTasks({ tier: args.tier, taskId: args.task });
if (!tasks.length) { console.error('No tasks found'); process.exit(1); }

const matrix = await runEval({ ...args, agentsByName, tracksByName, tasks, runId });

mkdirSync(join(resultsRoot, runId), { recursive: true });
writeFileSync(join(resultsRoot, runId, 'matrix.json'), JSON.stringify(matrix, null, 2));
const report = renderReport(matrix);
writeFileSync(join(resultsRoot, runId, 'report.md'), report);
console.log(report);
