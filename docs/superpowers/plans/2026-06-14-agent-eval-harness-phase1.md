# Agent Eval Harness — Phase 1 (Walking Skeleton) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a working, self-testing eval harness that runs a coding task in an isolated workspace against a pluggable agent, verifies the result with objective checks, and reports a pass/fail matrix — proven end-to-end by an oracle agent (must score 100%) and a no-op agent (must score 0%) on a trivial task.

**Architecture:** A zero-dependency Node ESM tool under `tools/agent-evals/`, mirroring `tools/build-agent-bundle.mjs` conventions. Three clean boundaries — **track** (provisions the workspace), **agent runner** (executes the task), **verifier** (runs objective checks) — wired by an orchestrator. The oracle/no-op fake agents are both the harness's test doubles and its correctness gate, so the whole pipeline is provable without spending money or running the .NET/Angular toolchain. The first real Spiderly task (`add-validator`, with a `dotnet build` verifier) lands last and runs out-of-band.

**Tech Stack:** Node 18+ ESM (`.mjs`), `node:test` (built-in test runner, zero deps), `node:child_process` for shelling out to agent/build CLIs. Source of truth for design: [`../specs/2026-06-14-agent-eval-harness-design.md`](../specs/2026-06-14-agent-eval-harness-design.md).

**Commit convention:** all commits in this plan use `feat(agent-evals): …` / `test(agent-evals): …` / `chore(agent-evals): …` and must carry the trailer `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Stage only the files each task touches.

**Working directory:** all paths are relative to the `spiderly/` repo root; run all commands from there. Repo is on the `develop` branch (correct per the spiderly workflow).

---

## File Structure

```
tools/agent-evals/
  lib/
    paths.mjs            # repo/dir path resolution (single source of paths)
    fs-utils.mjs         # copyDir helper
    exec.mjs             # run() — spawnSync wrapper returning {code, stdout, stderr}
  tracks/
    agnostic.mjs         # provision(task, ws): fixture + AGENTS.md index from the bundle
    agnostic.test.mjs
  agents/
    noop.mjs             # fake agent: does nothing (test double, must score 0%)
    oracle.mjs           # fake agent: applies oracle/<id> patch (test double, must score 100%)
    claude.mjs           # real agent: claude -p headless
    agents.test.mjs
  tasks/
    atomic/
      trivial-marker/    # self-test task (no toolchain needed)
        task.json  prompt.md  verify.mjs
      add-validator/     # first REAL Spiderly task (added in Task 9)
        task.json  prompt.md  verify.mjs
  oracle/
    trivial-marker/      # known-good patch for the self-test task
      result.txt
    add-validator/       # known-good patch for the real task (authored in Task 9)
  fixtures/
    trivial/             # trivial copyable fixture for the self-test
      README.md
    .gitkeep
  tasks-loader.mjs       # loadTasks({tier}) -> Task[]
  tasks-loader.test.mjs
  verify.mjs             # runVerify(task, ws) -> {checks}
  verify.test.mjs
  score.mjs              # scoreRow(task, verifyResult) -> {pass}
  run.mjs                # runEval(...) orchestrator
  run.test.mjs           # the oracle-100% / no-op-0% gate
  report.mjs             # renderReport(matrix) -> markdown
  report.test.mjs
  cli.mjs                # arg parsing + entrypoint
  results/               # gitignored ephemeral workspaces + matrix.json
```

**Shared data shapes** (JS objects; kept consistent across all tasks):

- `Task` = `{ id, tier, targets:string[], fixture:string, maxTurns:number, required:string[], dir:string, promptText:string }`
- `Check` = `{ name:string, pass:boolean, detail:string }`
- `AgentResult` = `{ transcript:string, tokens:number, costUsd:number, wallMs:number, turns:number, cleanExit:boolean }`
- `ResultRow` = `{ agent, track, taskId, rep, workspaceDir, agentMeta, checks:Check[], pass:(true|false|null), error:(string|null) }` — `pass:null` means an infra/harness error, never counted as an agent green/red.
- `Matrix` = `{ meta:{runId, track, reps, ...}, rows:ResultRow[] }`

---

## Task 1: Tool scaffold, path/fs/exec helpers, trivial fixture

**Files:**
- Create: `tools/agent-evals/lib/paths.mjs`
- Create: `tools/agent-evals/lib/fs-utils.mjs`
- Create: `tools/agent-evals/lib/exec.mjs`
- Create: `tools/agent-evals/lib/exec.test.mjs`
- Create: `tools/agent-evals/fixtures/trivial/README.md`
- Create: `tools/agent-evals/fixtures/.gitkeep`
- Modify: `tools/package.json` (add scripts)
- Modify: `.gitignore` (ignore ephemeral results)

- [ ] **Step 1: Create the path resolver**

`tools/agent-evals/lib/paths.mjs`:

```js
// Single source of truth for harness paths. Mirrors build-agent-bundle.mjs style.
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url)); // tools/agent-evals/lib
export const evalsRoot = resolve(here, '..');          // tools/agent-evals
export const repoRoot = resolve(evalsRoot, '..', '..'); // repo root

export const tasksRoot = join(evalsRoot, 'tasks');
export const fixturesRoot = join(evalsRoot, 'fixtures');
export const oracleRoot = join(evalsRoot, 'oracle');
export const resultsRoot = join(evalsRoot, 'results');

// The shipped agent bundle (committed build artifact); the agnostic track reads its manifest.
export const bundleRoot = join(repoRoot, 'Angular', 'projects', 'spiderly', 'agent');
```

- [ ] **Step 2: Create the fs helper**

`tools/agent-evals/lib/fs-utils.mjs`:

```js
import { cpSync, mkdirSync, existsSync } from 'node:fs';

// Recursively copy src into dst, merging onto any existing dst contents (used both to
// seed a fixture and to overlay an oracle patch).
export function copyDir(src, dst) {
  if (!existsSync(src)) throw new Error(`copyDir: source missing: ${src}`);
  mkdirSync(dst, { recursive: true });
  cpSync(src, dst, { recursive: true });
}
```

- [ ] **Step 3: Create the exec helper**

`tools/agent-evals/lib/exec.mjs`:

```js
import { spawnSync } from 'node:child_process';

// Run a command, capturing output. shell:true is needed on Windows for .cmd shims
// (npm, ng, claude). Returns a normalized {code, stdout, stderr}; never throws on
// non-zero exit (callers decide what a non-zero code means).
export function run(cmd, args = [], opts = {}) {
  const res = spawnSync(cmd, args, {
    cwd: opts.cwd,
    env: { ...process.env, ...opts.env },
    encoding: 'utf8',
    timeout: opts.timeoutMs ?? 0,
    shell: opts.shell ?? false,
    maxBuffer: 64 * 1024 * 1024,
  });
  return {
    code: res.status ?? (res.error ? -1 : 0),
    stdout: res.stdout ?? '',
    stderr: res.stderr ?? (res.error ? String(res.error) : ''),
  };
}
```

- [ ] **Step 4: Write the failing test for exec**

`tools/agent-evals/lib/exec.test.mjs`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { run } from './exec.mjs';

test('run captures stdout and a zero exit code', () => {
  const r = run(process.execPath, ['-e', "process.stdout.write('hi')"]);
  assert.equal(r.code, 0);
  assert.equal(r.stdout, 'hi');
});

test('run reports a non-zero exit code without throwing', () => {
  const r = run(process.execPath, ['-e', 'process.exit(3)']);
  assert.equal(r.code, 3);
});
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/lib/`
Expected: PASS, 2 tests passing. (Implementation already written in Step 3 — this confirms the helper.)

- [ ] **Step 6: Create the trivial fixture**

`tools/agent-evals/fixtures/trivial/README.md`:

```md
Trivial eval fixture. The self-test task asks an agent to create `result.txt`
containing `DONE` in this workspace; no .NET/Angular toolchain involved.
```

`tools/agent-evals/fixtures/.gitkeep`: (empty file)

- [ ] **Step 7: Add npm scripts**

Modify `tools/package.json` — add to the `"scripts"` object:

```json
    "eval": "node agent-evals/cli.mjs",
    "test:evals": "node --test agent-evals/"
```

- [ ] **Step 8: Ignore ephemeral results**

Append to `.gitignore` (repo root):

```
# Agent-eval ephemeral run workspaces (results committed in later phases go elsewhere)
tools/agent-evals/results/
```

- [ ] **Step 9: Commit**

```bash
git add tools/agent-evals/lib tools/agent-evals/fixtures tools/package.json .gitignore
git commit -m "feat(agent-evals): scaffold harness paths, fs/exec helpers, trivial fixture"
```

---

## Task 2: Task loader

**Files:**
- Create: `tools/agent-evals/tasks-loader.mjs`
- Create: `tools/agent-evals/tasks/atomic/trivial-marker/task.json`
- Create: `tools/agent-evals/tasks/atomic/trivial-marker/prompt.md`
- Create: `tools/agent-evals/tasks-loader.test.mjs`

- [ ] **Step 1: Create the trivial task definition**

`tools/agent-evals/tasks/atomic/trivial-marker/task.json`:

```json
{
  "id": "trivial-marker",
  "tier": "atomic",
  "targets": ["self-test"],
  "fixture": "trivial",
  "maxTurns": 1,
  "required": ["marker-present"]
}
```

`tools/agent-evals/tasks/atomic/trivial-marker/prompt.md`:

```md
Create a file named `result.txt` in the current directory containing exactly the text `DONE` (no quotes, no trailing newline required).
```

- [ ] **Step 2: Write the failing test**

`tools/agent-evals/tasks-loader.test.mjs`:

```js
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
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/tasks-loader.test.mjs`
Expected: FAIL — `Cannot find module './tasks-loader.mjs'`.

- [ ] **Step 4: Implement the loader**

`tools/agent-evals/tasks-loader.mjs`:

```js
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tasksRoot } from './lib/paths.mjs';

const TIERS = ['atomic', 'feature', 'full-app'];
const REQUIRED_FIELDS = ['id', 'tier', 'targets', 'fixture', 'maxTurns', 'required'];

// Discover tasks. A task is a folder under tasks/<tier>/ containing task.json + prompt.md.
export function loadTasks({ tier } = {}) {
  const tasks = [];
  for (const t of TIERS) {
    if (tier && tier !== t) continue;
    const tierDir = join(tasksRoot, t);
    if (!existsSync(tierDir)) continue;
    for (const d of readdirSync(tierDir, { withFileTypes: true })) {
      if (!d.isDirectory()) continue;
      const dir = join(tierDir, d.name);
      if (!existsSync(join(dir, 'task.json'))) continue;
      const meta = JSON.parse(readFileSync(join(dir, 'task.json'), 'utf8'));
      for (const f of REQUIRED_FIELDS) {
        if (meta[f] === undefined) throw new Error(`task ${d.name}: missing field "${f}"`);
      }
      if (meta.id !== d.name) {
        throw new Error(`task ${d.name}: id "${meta.id}" must equal folder name`);
      }
      const promptText = readFileSync(join(dir, 'prompt.md'), 'utf8');
      tasks.push({ ...meta, dir, promptText });
    }
  }
  return tasks;
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/tasks-loader.test.mjs`
Expected: PASS, 1 test passing.

- [ ] **Step 6: Commit**

```bash
git add tools/agent-evals/tasks-loader.mjs tools/agent-evals/tasks-loader.test.mjs tools/agent-evals/tasks/atomic/trivial-marker
git commit -m "feat(agent-evals): task loader + trivial-marker self-test task"
```

---

## Task 3: Agnostic track provisioning

**Files:**
- Create: `tools/agent-evals/tracks/agnostic.mjs`
- Create: `tools/agent-evals/tracks/agnostic.test.mjs`

- [ ] **Step 1: Write the failing test**

`tools/agent-evals/tracks/agnostic.test.mjs`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, mkdirSync, writeFileSync, readFileSync, existsSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import { provision } from './agnostic.mjs';

test('agnostic provision copies the fixture and writes an AGENTS.md index of doc surfaces only', () => {
  // Fake bundle with one doc + one skill surface.
  const bundle = mkdtempSync(join(tmpdir(), 'bundle-'));
  writeFileSync(join(bundle, 'manifest.json'), JSON.stringify({
    skills: [
      { name: 'entity-design', surface: 'doc', description: 'Design entities.' },
      { name: 'deployment', surface: 'skill', description: 'Deploy the app.' },
    ],
  }));
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  // task.fixture 'trivial' resolves under the harness fixturesRoot (exists from Task 1).
  provision({ fixture: 'trivial' }, ws, { bundlePath: bundle });

  assert.ok(existsSync(join(ws, 'README.md')), 'fixture file copied');
  const agents = readFileSync(join(ws, 'AGENTS.md'), 'utf8');
  assert.match(agents, /entity-design/, 'doc surface listed');
  assert.doesNotMatch(agents, /deployment/, 'skill surface must NOT be in the agnostic index');
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/tracks/agnostic.test.mjs`
Expected: FAIL — `Cannot find module './agnostic.mjs'`.

- [ ] **Step 3: Implement the agnostic track**

`tools/agent-evals/tracks/agnostic.mjs`:

```js
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot, bundleRoot } from '../lib/paths.mjs';

// Agnostic track: every agent gets ONLY the AGENTS.md index built from the shipped
// bundle's `doc`-surface entries — no agent-specific layer (no skill junctions, no
// .cursor/rules). bundlePath is injectable for testing.
export function provision(task, workspaceDir, { bundlePath = bundleRoot } = {}) {
  copyDir(join(fixturesRoot, task.fixture), workspaceDir);

  const manifestPath = join(bundlePath, 'manifest.json');
  if (!existsSync(manifestPath)) return; // no bundle (e.g. trivial self-test) → fixture only

  const skills = JSON.parse(readFileSync(manifestPath, 'utf8')).skills ?? [];
  const docs = skills.filter((s) => s.surface === 'doc');
  const index = [
    '# Spiderly agent guidance (agnostic track)',
    '',
    ...docs.map((d) => `- **${d.name}** — ${d.description}`),
  ].join('\n');
  writeFileSync(join(workspaceDir, 'AGENTS.md'), index + '\n');
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/tracks/agnostic.test.mjs`
Expected: PASS, 1 test passing.

- [ ] **Step 5: Commit**

```bash
git add tools/agent-evals/tracks
git commit -m "feat(agent-evals): agnostic track provisioning (fixture + AGENTS.md index)"
```

---

## Task 4: Verifier runner + trivial task verifier

**Files:**
- Create: `tools/agent-evals/verify.mjs`
- Create: `tools/agent-evals/tasks/atomic/trivial-marker/verify.mjs`
- Create: `tools/agent-evals/verify.test.mjs`

- [ ] **Step 1: Create the trivial task's verifier**

`tools/agent-evals/tasks/atomic/trivial-marker/verify.mjs`:

```js
import { existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

// A task verifier default-exports async ({ workspaceDir, run }) => Check[]
// where Check = { name, pass, detail }. This one needs no toolchain.
export default async function verify({ workspaceDir }) {
  const p = join(workspaceDir, 'result.txt');
  const ok = existsSync(p) && readFileSync(p, 'utf8').trim() === 'DONE';
  return [{
    name: 'marker-present',
    pass: ok,
    detail: ok ? 'result.txt == DONE' : 'result.txt missing or wrong content',
  }];
}
```

- [ ] **Step 2: Write the failing test**

`tools/agent-evals/verify.test.mjs`:

```js
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
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/verify.test.mjs`
Expected: FAIL — `Cannot find module './verify.mjs'`.

- [ ] **Step 4: Implement the verifier runner**

`tools/agent-evals/verify.mjs`:

```js
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { run } from './lib/exec.mjs';

// Loads the task's own verify.mjs and runs it, passing the shared exec `run` helper so
// verifiers can shell out to dotnet/ng/playwright. Returns { checks: Check[] }.
export async function runVerify(task, workspaceDir) {
  const verifyPath = join(task.dir, 'verify.mjs');
  if (!existsSync(verifyPath)) throw new Error(`task ${task.id}: missing verify.mjs`);
  const mod = await import(pathToFileURL(verifyPath).href);
  const checks = await mod.default({ workspaceDir, run });
  if (!Array.isArray(checks)) {
    throw new Error(`task ${task.id}: verify.mjs must return an array of checks`);
  }
  return { checks };
}
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/verify.test.mjs`
Expected: PASS, 2 tests passing.

- [ ] **Step 6: Commit**

```bash
git add tools/agent-evals/verify.mjs tools/agent-evals/verify.test.mjs tools/agent-evals/tasks/atomic/trivial-marker/verify.mjs
git commit -m "feat(agent-evals): verifier runner + trivial task verifier"
```

---

## Task 5: Fake agents (no-op, oracle)

**Files:**
- Create: `tools/agent-evals/agents/noop.mjs`
- Create: `tools/agent-evals/agents/oracle.mjs`
- Create: `tools/agent-evals/oracle/trivial-marker/result.txt`
- Create: `tools/agent-evals/agents/agents.test.mjs`

- [ ] **Step 1: Create the no-op agent**

`tools/agent-evals/agents/noop.mjs`:

```js
// Fake agent that does nothing. Must score ~0% — proves verifiers are not too lax.
export default {
  name: 'noop',
  async run() {
    return { transcript: '', tokens: 0, costUsd: 0, wallMs: 0, turns: 0, cleanExit: true };
  },
};
```

- [ ] **Step 2: Create the oracle agent and its trivial patch**

`tools/agent-evals/agents/oracle.mjs`:

```js
import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { oracleRoot } from '../lib/paths.mjs';

// Fake agent that overlays the known-good patch at oracle/<taskId>/ onto the workspace.
// Must score 100% — proves verifiers are not too strict. The patch doubles as living
// documentation of the task's intended solution.
export default {
  name: 'oracle',
  async run({ task, workspaceDir }) {
    const patch = join(oracleRoot, task.id);
    if (!existsSync(patch)) throw new Error(`oracle: no patch for task ${task.id} at ${patch}`);
    copyDir(patch, workspaceDir);
    return { transcript: '[oracle applied patch]', tokens: 0, costUsd: 0, wallMs: 0, turns: 1, cleanExit: true };
  },
};
```

`tools/agent-evals/oracle/trivial-marker/result.txt` (exact content — the trailing newline is harmless because the verifier trims):

```
DONE
```

- [ ] **Step 3: Write the failing test**

`tools/agent-evals/agents/agents.test.mjs`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtempSync, existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { tmpdir } from 'node:os';
import noop from './noop.mjs';
import oracle from './oracle.mjs';

test('noop agent makes no changes', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  await noop.run({ task: { id: 'trivial-marker' }, workspaceDir: ws });
  assert.equal(existsSync(join(ws, 'result.txt')), false);
});

test('oracle agent applies the trivial-marker patch', async () => {
  const ws = mkdtempSync(join(tmpdir(), 'ws-'));
  await oracle.run({ task: { id: 'trivial-marker' }, workspaceDir: ws });
  assert.equal(readFileSync(join(ws, 'result.txt'), 'utf8').trim(), 'DONE');
});
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/agents/agents.test.mjs`
Expected: FAIL — `Cannot find module './noop.mjs'` (or `./oracle.mjs`).

- [ ] **Step 5: (Implementation already written in Steps 1–2.) Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/agents/agents.test.mjs`
Expected: PASS, 2 tests passing.

- [ ] **Step 6: Commit**

```bash
git add tools/agent-evals/agents/noop.mjs tools/agent-evals/agents/oracle.mjs tools/agent-evals/agents/agents.test.mjs tools/agent-evals/oracle/trivial-marker
git commit -m "feat(agent-evals): no-op and oracle fake agents + trivial oracle patch"
```

---

## Task 6: Scorer + orchestrator + the self-test gate

**Files:**
- Create: `tools/agent-evals/score.mjs`
- Create: `tools/agent-evals/run.mjs`
- Create: `tools/agent-evals/run.test.mjs`

- [ ] **Step 1: Create the scorer**

`tools/agent-evals/score.mjs`:

```js
// Headline score: a task is green iff ALL its `required` checks passed.
// Sub-checks are retained on the row for diagnosis but never averaged into the headline.
export function scoreRow(task, verifyResult) {
  const byName = new Map(verifyResult.checks.map((c) => [c.name, c.pass]));
  const pass = task.required.every((name) => byName.get(name) === true);
  return { pass };
}
```

- [ ] **Step 2: Create the orchestrator**

`tools/agent-evals/run.mjs`:

```js
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
```

- [ ] **Step 3: Write the failing self-test (the gate)**

`tools/agent-evals/run.test.mjs`:

```js
import { test } from 'node:test';
import assert from 'node:assert/strict';
import { loadTasks } from './tasks-loader.mjs';
import { runEval } from './run.mjs';
import noop from './agents/noop.mjs';
import oracle from './agents/oracle.mjs';

test('GATE: oracle scores 100% and no-op scores 0% on the trivial task', async () => {
  const tasks = loadTasks({ tier: 'atomic' }).filter((t) => t.id === 'trivial-marker');
  assert.equal(tasks.length, 1);

  const m = await runEval({
    agents: ['oracle', 'noop'],
    agentsByName: { oracle, noop },
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
```

- [ ] **Step 4: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/run.test.mjs`
Expected: FAIL — `Cannot find module './run.mjs'` (if run before Step 2 saved) or, once present, PASS. If it fails with assertion errors, the harness does NOT discriminate — stop and fix before proceeding.

- [ ] **Step 5: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/run.test.mjs`
Expected: PASS, 1 test passing. This is the proof the pipeline discriminates correct from incorrect work.

- [ ] **Step 6: Commit**

```bash
git add tools/agent-evals/score.mjs tools/agent-evals/run.mjs tools/agent-evals/run.test.mjs
git commit -m "feat(agent-evals): scorer + orchestrator + oracle/no-op self-test gate"
```

---

## Task 7: Report renderer

**Files:**
- Create: `tools/agent-evals/report.mjs`
- Create: `tools/agent-evals/report.test.mjs`

- [ ] **Step 1: Write the failing test**

`tools/agent-evals/report.test.mjs`:

```js
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
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd tools && node --test agent-evals/report.test.mjs`
Expected: FAIL — `Cannot find module './report.mjs'`.

- [ ] **Step 3: Implement the report renderer**

`tools/agent-evals/report.mjs`:

```js
// Render a Matrix into a markdown summary table + a failure digest grouped by task.
// Pass rate counts only true/false rows; pass:null (infra) is tallied separately.
export function renderReport(matrix) {
  const groups = new Map();
  for (const r of matrix.rows) {
    const k = `${r.agent}__${r.taskId}`;
    if (!groups.has(k)) groups.set(k, { agent: r.agent, taskId: r.taskId, pass: 0, total: 0, infra: 0 });
    const g = groups.get(k);
    if (r.pass === null) g.infra++;
    else { g.total++; if (r.pass) g.pass++; }
  }

  const lines = [
    `# Eval report — ${matrix.meta.runId}`,
    '',
    `Track: ${matrix.meta.track} · reps: ${matrix.meta.reps}`,
    '',
    '| Agent | Task | Pass rate | Infra errors |',
    '|---|---|---|---|',
  ];
  for (const g of [...groups.values()].sort((a, b) => (a.agent + a.taskId).localeCompare(b.agent + b.taskId))) {
    lines.push(`| ${g.agent} | ${g.taskId} | ${g.pass}/${g.total} | ${g.infra} |`);
  }

  const fails = matrix.rows.filter((r) => r.pass === false);
  if (fails.length) {
    lines.push('', '## Failure digest', '');
    for (const r of fails) {
      const failed = (r.checks ?? []).filter((c) => !c.pass).map((c) => c.name).join(', ') || '(no checks)';
      lines.push(`- **${r.agent}/${r.taskId}** rep ${r.rep} — failed: ${failed}`);
    }
  }
  return lines.join('\n');
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd tools && node --test agent-evals/report.test.mjs`
Expected: PASS, 1 test passing.

- [ ] **Step 5: Commit**

```bash
git add tools/agent-evals/report.mjs tools/agent-evals/report.test.mjs
git commit -m "feat(agent-evals): markdown report renderer with failure digest"
```

---

## Task 8: CLI entrypoint + real Claude adapter

**Files:**
- Create: `tools/agent-evals/agents/claude.mjs`
- Create: `tools/agent-evals/cli.mjs`

- [ ] **Step 1: Confirm the Claude headless flags**

Run: `claude --help`
Note the exact flags for: print mode (`-p`/`--print`), `--output-format json`, `--max-turns`, and the non-interactive permission flag (expected `--permission-mode bypassPermissions`, or `--dangerously-skip-permissions`). CLI flags evolve — if they differ from Step 2's code, update Step 2 to match before running anything real. (Project rule: verify external tool/API surface rather than trusting memory.)

- [ ] **Step 2: Implement the Claude adapter**

`tools/agent-evals/agents/claude.mjs`:

```js
import { run } from '../lib/exec.mjs';

// Real Claude Code headless run. Edits the workspace in place over multiple turns.
// Requires `claude` on PATH and credentials (logged-in CLI or ANTHROPIC_API_KEY) in env.
// NOTE: Date.now() is fine here — this is an ordinary Node CLI, not a Workflow() sandbox.
export default {
  name: 'claude',
  async run({ workspaceDir, prompt, maxTurns }) {
    const start = Date.now();
    const res = run('claude', [
      '-p', prompt,
      '--output-format', 'json',
      '--max-turns', String(maxTurns ?? 20),
      '--permission-mode', 'bypassPermissions',
    ], { cwd: workspaceDir, shell: true, timeoutMs: 20 * 60 * 1000 });
    const wallMs = Date.now() - start;

    let meta = { tokens: 0, costUsd: 0, turns: 0 };
    try {
      const j = JSON.parse(res.stdout);
      meta = {
        tokens: j.usage?.output_tokens ?? 0,
        costUsd: j.total_cost_usd ?? 0,
        turns: j.num_turns ?? 0,
      };
    } catch { /* non-JSON output (e.g. crash) — keep defaults, transcript still captured */ }

    return { transcript: res.stdout, ...meta, wallMs, cleanExit: res.code === 0 };
  },
};
```

- [ ] **Step 3: Implement the CLI**

`tools/agent-evals/cli.mjs`:

```js
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
```

- [ ] **Step 4: Smoke-test the CLI end-to-end with the fake agents (no cost, no toolchain)**

Run: `cd tools && node agent-evals/cli.mjs run --agents oracle,noop --track agnostic --tier atomic --reps 1`
Expected output: a markdown table showing `oracle | trivial-marker | 1/1 | 0` and `noop | trivial-marker | 0/1 | 0`, plus a failure digest line for the no-op. A `matrix.json` is written under `tools/agent-evals/results/<runId>/`.

- [ ] **Step 5: Commit**

```bash
git add tools/agent-evals/agents/claude.mjs tools/agent-evals/cli.mjs
git commit -m "feat(agent-evals): CLI entrypoint + real Claude headless adapter"
```

---

## Task 9: First real Spiderly task (`add-validator`) — out-of-band

> This task adds the first *real* corpus entry. Its verifier needs the .NET/Angular toolchain and a scaffolded Spiderly app, and a real Claude run costs money — so it runs **out-of-band** (manual), never as part of `node --test`. This matches the spec's validation strategy.

**Files:**
- Create: `tools/agent-evals/tasks/atomic/add-validator/task.json`
- Create: `tools/agent-evals/tasks/atomic/add-validator/prompt.md`
- Create: `tools/agent-evals/tasks/atomic/add-validator/verify.mjs`
- Create: `tools/agent-evals/oracle/add-validator/` (snapshotted known-good patch)
- Create: `tools/agent-evals/fixtures/prepare-spiderly-app.md` (one-time fixture prep notes)

- [ ] **Step 1: Document the real-fixture prep**

`tools/agent-evals/fixtures/prepare-spiderly-app.md`:

```md
# Preparing the `spiderly-app` fixture (one-time, local)

The e2e overlay (`tests/e2e-fixtures/setup.sh`) populates an already-init'd app, so the
fixture must be scaffolded first, then committed-light or kept local (gitignored).

1. From a scratch dir: `spiderly init` to scaffold `Backend/` + `Frontend/`.
2. Run `tests/e2e-fixtures/setup.sh <AppName> <appFolder>` to overlay entities (Product, etc.).
3. `npm install` in `Frontend/` and `dotnet restore` in `Backend/` so builds are warm.
4. Copy the result to `tools/agent-evals/fixtures/spiderly-app/`.

This fixture is large; keep it gitignored (add `tools/agent-evals/fixtures/spiderly-app/`
to .gitignore) and document the regen steps here rather than committing the whole app.
```

Then add to `.gitignore`:

```
tools/agent-evals/fixtures/spiderly-app/
```

- [ ] **Step 2: Create the task definition**

`tools/agent-evals/tasks/atomic/add-validator/task.json`:

```json
{
  "id": "add-validator",
  "tier": "atomic",
  "targets": ["validation"],
  "fixture": "spiderly-app",
  "maxTurns": 20,
  "required": ["compiles"]
}
```

`tools/agent-evals/tasks/atomic/add-validator/prompt.md`:

```md
In the Spiderly application in this workspace, add a validation rule for the `Product` entity so that its `Name` property is required (non-empty) and at most 100 characters long. Use Spiderly's built-in validation mechanism — do not hand-roll validation outside the framework's conventions.
```

- [ ] **Step 3: Create the verifier**

`tools/agent-evals/tasks/atomic/add-validator/verify.mjs`:

```js
import { join } from 'node:path';

// `compiles` is the required, deterministic signal — Spiderly's source generators run during
// dotnet build, so a malformed validator fails the compile. `validator-present` is a diagnostic.
export default async function verify({ workspaceDir, run }) {
  const backend = join(workspaceDir, 'Backend');
  const build = run('dotnet', ['build'], { cwd: backend, shell: true, timeoutMs: 10 * 60 * 1000 });
  const compiles = build.code === 0;

  // Heuristic diagnostic: did they touch a validation rule for Product.Name at all?
  const grep = run('grep', ['-rIl', '--include=*.cs', 'RuleFor', backend], { shell: true });
  const validatorPresent = grep.code === 0 && /name/i.test(grep.stdout + build.stdout);

  return [
    { name: 'compiles', pass: compiles, detail: compiles ? 'dotnet build OK' : build.stderr.slice(-500) },
    { name: 'validator-present', pass: validatorPresent, detail: 'found RuleFor reference (heuristic)' },
  ];
}
```

- [ ] **Step 4: Author the oracle patch by doing the task once, by hand**

Do not invent the validator code. Instead:
1. Provision a workspace from the `spiderly-app` fixture manually (copy it to a scratch dir).
2. Implement the Product.Name validation the Spiderly way (consult the `validation` doc surface / `attribute-reference`).
3. Run the verifier's `dotnet build` and confirm `compiles` passes.
4. Copy ONLY the files you changed into `tools/agent-evals/oracle/add-validator/`, preserving their relative paths under the workspace root (e.g. `Backend/<App>.Business/ValidationRules/...`).

The oracle patch is the authoritative known-good solution and doubles as documentation.

- [ ] **Step 5: Validate the harness against the real task (oracle must pass, no-op must fail)**

Run: `cd tools && node agent-evals/cli.mjs run --agents oracle,noop --track agnostic --tier atomic --reps 1`
Expected: `oracle | add-validator | 1/1` and `noop | add-validator | 0/1`. (Also re-runs trivial-marker.) If the oracle fails `compiles`, the oracle patch or fixture is wrong — fix before any paid run.

- [ ] **Step 6: First real Claude run (out-of-band, costs money)**

Run: `cd tools && node agent-evals/cli.mjs run --agents claude --track agnostic --tier atomic --reps 3`
Expected: a pass-rate row for `claude | add-validator | N/3` plus cost/turns captured in `matrix.json`. Read the failure digest; if Claude stumbles, that is the first real signal to improve the `validation` doc surface — the whole point of Phase 1.

- [ ] **Step 7: Commit (task definition + oracle patch only; not the fixture or results)**

```bash
git add tools/agent-evals/tasks/atomic/add-validator tools/agent-evals/oracle/add-validator tools/agent-evals/fixtures/prepare-spiderly-app.md .gitignore
git commit -m "feat(agent-evals): first real task add-validator (dotnet-build verifier, out-of-band)"
```

---

## Final verification

- [ ] **Run the full deterministic suite** — Run: `cd tools && node --test agent-evals/` · Expected: all tests pass (exec, tasks-loader, agnostic, verify, agents, run-gate, report). The `add-validator` real task is NOT exercised here (no `*.test.mjs`), by design.
- [ ] **Confirm the gate** — the `run.test.mjs` GATE test is green (oracle 100%, no-op 0%). If it ever goes red, the harness has stopped discriminating — treat as a release blocker for the harness itself.

---

## Self-Review

**Spec coverage** (against `../specs/2026-06-14-agent-eval-harness-design.md`):
- Three boundaries (track/runner/verifier) → Tasks 3, 5/8, 4. ✓
- Graded-mix task format with `targets`/`required` → Task 2 (format) + Tasks 4, 9 (atomic instances). *Note: feature/full-app tiers are deferred to a later plan; the loader already supports them (Task 2).*
- Agnostic track = bundle's `doc` surfaces → Task 3. ✓
- Oracle/no-op self-test → Tasks 5–6. ✓
- Binary headline score + sub-checks for diagnosis → Task 6 (`score.mjs`) + Task 9 (`compiles` required, `validator-present` diagnostic). ✓
- Stochasticity via reps + pass rate → `reps` flag (Task 8) + pass-rate report (Task 7). ✓
- Metrics beyond pass/fail (cost/turns/wall) → captured in `agentMeta` (Tasks 6, 8). *Hack-detector + reproducibility metadata stamping are deferred to Phase 2/3 — out of Phase-1 scope.*
- Infra-vs-agent failure distinction → Task 6 (`pass:null`). ✓
- Out-of-band real runs, no live blocking gate → Task 9 (manual). ✓
- **Deferred to later plans (correctly out of Phase-1 scope):** native track + cursor/gemini/codex adapters + generated per-agent files (Phase 2); committed results + freshness gate (Phase 3); doc-body copying into the agnostic workspace and feature/full-app tasks.

**Placeholder scan:** no TBD/TODO; every code step has complete code; the one "author by hand" step (Task 9 Step 4) is deliberate — inventing Spiderly validator code here risks a wrong API, and the spec explicitly makes oracle patches hand-snapshotted living documentation.

**Type/name consistency:** `Check{name,pass,detail}`, `AgentResult{transcript,tokens,costUsd,wallMs,turns,cleanExit}`, `runEval({agents,agentsByName,track,tasks,reps,runId,meta})`, `provision(task,ws,{bundlePath})`, `adapter.run({task,workspaceDir,prompt,maxTurns})`, `scoreRow(task,verifyResult)`, `runVerify(task,ws)` — all used identically across tasks. ✓
