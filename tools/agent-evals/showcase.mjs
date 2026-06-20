#!/usr/bin/env node
import { mkdirSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { resultsRoot } from './lib/paths.mjs';
import { walkFiles } from './lib/fs-utils.mjs';
import claude from './agents/claude.mjs';
import codex from './agents/codex.mjs';
import noop from './agents/noop.mjs';
import { provision as provisionPlain } from './tracks/plain.mjs';
import { provision as provisionNative } from './tracks/native.mjs';

// Showcase orchestrator — the UNSCORED demo-video sibling of the scored eval (cli.mjs).
// Runs {claude} × {spiderly, plain} (codex deferred for the first cut) as freestyle whole-app builds from a one-line
// prompt, then records each built workspace + metrics + file tree for the video (HUD + file-tree
// bloom). No pass/fail, no verify.mjs, no reps — see the showcase-track design section in
// docs/superpowers/specs/2026-06-14-agent-eval-harness-design.md.
//
// spiderly vs plain differ by provisioning (see PROVISION below) and by whether the Spiderly
// toolchain is on PATH for the spiderly cell (CLI + packages, so `spiderly init` works) — the
// caller's job (locally: an installed CLI; in CI: the agent-evals.yml build+publish).

const agentsByName = { claude, codex, noop };

// Named domain (vs a bare "make the app") so all four builds share a comparable entity and the
// walkthrough + recap grid are meaningful. The two phrasings are the locked showcase prompts.
const DOMAIN = 'product-catalog admin panel';
const PROMPTS = {
  spiderly: `Make a ${DOMAIN} with Spiderly.`,
  plain: `Make a ${DOMAIN}.`,
};
const SIDES = ['spiderly', 'plain'];

// Per-side provisioning: the spiderly cell gets the real Claude+Spiderly guidance (native track —
// AGENTS.md docs pointer + .claude/skills); the plain cell gets a bare workspace (no framework).
// The agent does its own scaffolding either way (`spiderly init` vs `dotnet new` / `ng new`).
const PROVISION = { spiderly: provisionNative, plain: provisionPlain };

// A whole-app build needs far more turns than an atomic task. codex ignores maxTurns (no exec flag);
// claude honours it. NOTE: the adapters' hard 20-min wall-clock timeout is the likely binding limit
// for a full freestyle build — making that configurable is a known follow-up once a first real run
// shows whether it truncates.
const SHOWCASE_MAX_TURNS = 200;

export { PROMPTS };

function parseArgs(argv) {
  // Codex is deferred for the first cut — default to Claude only. Pass `--agents claude,codex`
  // (once codex is re-auth'd) to run the full 2×2.
  const a = { agents: ['claude'], sides: [...SIDES] };
  for (let i = 0; i < argv.length; i++) {
    const k = argv[i].startsWith('--') ? argv[i].slice(2) : null;
    const v = argv[i + 1];
    if (!k || v === undefined || v.startsWith('--')) { console.error(`showcase: bad arg near ${argv[i]}`); process.exit(1); }
    if (k === 'agents') { a.agents = v.split(',').filter(Boolean); i++; }
    else if (k === 'sides') { a.sides = v.split(',').filter(Boolean); i++; }
    else { console.error(`showcase: unknown flag --${k}`); process.exit(1); }
  }
  return a;
}

// Only orchestrate when run directly (`node showcase.mjs ...`) — importing for tests must not run it.
if (import.meta.url === pathToFileURL(process.argv[1]).href) {
  const { agents, sides } = parseArgs(process.argv.slice(2));
  const runId = new Date().toISOString().replace(/[:.]/g, '-');
  const root = join(resultsRoot, `showcase-${runId}`);
  const cells = [];

  for (const agentName of agents) {
    const adapter = agentsByName[agentName];
    if (!adapter) { console.error(`showcase: unknown agent "${agentName}"`); process.exit(1); }
    for (const side of sides) {
      const prompt = PROMPTS[side];
      if (!prompt) { console.error(`showcase: unknown side "${side}"`); process.exit(1); }
      const ws = join(root, 'ws', `${agentName}-${side}`);
      mkdirSync(ws, { recursive: true });
      PROVISION[side]({ fixture: 'showcase-empty' }, ws); // spiderly→native guidance, plain→bare

      console.log(`[showcase] ${agentName} × ${side} — building…`);
      let meta = { tokens: 0, turns: 0, wallMs: 0, cleanExit: false, costUsd: 0 };
      let transcript = '';
      let error = null;
      try {
        const r = await adapter.run({ task: { id: `${agentName}-${side}` }, workspaceDir: ws, prompt, maxTurns: SHOWCASE_MAX_TURNS });
        meta = { tokens: r.tokens ?? 0, turns: r.turns ?? 0, wallMs: r.wallMs ?? 0, cleanExit: !!r.cleanExit, costUsd: r.costUsd ?? 0 };
        transcript = r.transcript ?? '';
      } catch (err) {
        error = String(err?.message ?? err);
      }

      const files = walkFiles(ws);
      writeFileSync(join(root, `${agentName}-${side}.transcript.txt`), transcript);
      cells.push({ agent: agentName, side, prompt, ...meta, fileCount: files.length, files, error });
      console.log(`[showcase] ${agentName} × ${side} — ${error ? `ERROR: ${error}` : `${files.length} files · ${meta.turns} turns · ${(meta.wallMs / 1000).toFixed(0)}s`}`);
    }
  }

  writeFileSync(join(root, 'showcase.json'), JSON.stringify({ runId, cells }, null, 2));
  // ws/ holds full built apps (large — gitignore-worthy); showcase.json is the small artifact the
  // video pipeline reads for the HUD/bloom, and ws/ is what gets booted + recorded.
  console.log(`\n[showcase] wrote ${join(root, 'showcase.json')} — ${cells.length} cells`);
}
