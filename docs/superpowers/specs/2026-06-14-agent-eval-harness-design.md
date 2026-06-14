# Agent eval harness — Design

> Status: **PROPOSED.** Concretizes the *Eval / validation strategy* section of
> [`docs/agent-guidance-distribution.md`](../../agent-guidance-distribution.md) (issue
> [#250](https://github.com/filiptrivan/spiderly/issues/250)). That doc decided *how* agent guidance
> ships (versioned npm package → `AGENTS.md` index + junctioned skills, reconciled by
> `Spiderly.CLI agent-sync`, categorized per skill in `tools/agent-surface.json`). This doc decides
> *how we measure whether that guidance actually works* — for Claude Code and for the other agents
> Spiderly now claims to support (Cursor, Gemini, Codex).

## Problem

Spiderly invests heavily in agent guidance — 17 categorized surfaces (10 `doc`, 7 `skill`), a
generated bundle (`tools/build-agent-bundle.mjs` → `Angular/projects/spiderly/agent/`), and a
projector (`agent-sync`). **None of it is measured.** We do not know:

- whether an agent can actually complete a Spiderly task using the shipped bundle;
- whether the `doc`-vs-`skill` split in `agent-surface.json` is correct (Open decision #3 in the
  distribution doc explicitly defers `backend-hooks` / `authorization` placement to "revisit if evals
  say otherwise" — there are currently no evals to say otherwise);
- whether the non-Claude agents we now target (Cursor, Gemini, Codex) get a comparable experience
  from the `AGENTS.md` index alone, since the junctioned skills are Claude-only.

The published Vercel eval that motivated the distribution design — `AGENTS.md` + bundled docs **100%**,
skills-with-instructions **79%**, skills **failed to trigger 56%** of the time
([writeup](https://vercel.com/blog/agents-md-outperforms-skills-in-our-agent-evals)) — was run on
*Next.js*, not Spiderly. We adopted its conclusion on faith. This harness lets us verify it on our own
framework and corpus.

## Why Spiderly is unusually well-suited to objective evals

Most frameworks must invent fuzzy "is the output good" rubrics. Spiderly does not: **source generators
run during `dotnet build`**, so a malformed entity or a misused attribute fails the compile
deterministically. Layering `dotnet ef database update`, `ng build`, and the existing Playwright
`tests/e2e-fixtures` on top yields a hard, non-subjective pass/fail at every layer. The design leans on
compiler/test outcomes as ground truth and avoids LLM-judge grading except where a compiler structurally
cannot check the thing.

## Goals (one harness, three phased uses)

The harness — a task corpus + an objective verifier + an agent runner — is built once. The three goals
are different ways of *running* it:

1. **Improve the guidance bundle.** Find where agents stumble, fix the bundled docs/skills, re-measure.
   (Phase 1, mostly Claude Code.)
2. **Cross-agent portability.** Prove Cursor / Gemini / Codex users get comparable results from the
   `AGENTS.md` index, not just Claude Code users. Produce a portability matrix and the
   agnostic↔native gap. (Phase 2.)
3. **Regression signal.** Commit eval results and gate cheaply + deterministically on their freshness —
   **not** a live blocking gate (see *Validation strategy*). (Phase 3.)

## Non-goals

- **No LLM-judge as the headline metric.** Compiler/test outcomes are the truth; an LLM judge is at most
  a secondary signal for things a compiler cannot check.
- **No blocking per-PR/per-release gate on live agent runs.** Non-deterministic and costly; it would go
  red on noise. (Decided in the distribution doc; restated here.)
- **Not a benchmark to publish a leaderboard.** The output is *actionable gaps* and a portability
  picture for our own decisions, not marketing rankings (though the portability matrix can back a
  "works with X" claim).

## Architecture

Lives beside the existing agent tooling, reusing the `tools/` Node setup:

```
spiderly/tools/agent-evals/
  cli.mjs                 # node cli.mjs run --agents claude,cursor --track agnostic --tier atomic --reps 3
  agents/                 # one runner adapter per agent — the ONLY agent-specific code
    claude.mjs  cursor.mjs  gemini.mjs  codex.mjs
  tracks/                 # context provisioning into the workspace, before the agent runs
    agnostic.mjs          #   AGENTS.md index + bundled docs only (what every agent gets)
    native.mjs            #   agnostic + the agent's own layer (Claude: junctioned skills; etc.)
  tasks/
    atomic/   <task>/ {task.json, prompt.md, verify.mjs}
    feature/  <task>/ ...
    full-app/ <task>/ ...
  oracle/                 # known-good patch per task — the harness's own self-test
  results/<run-id>/matrix.json
  report.mjs              # matrix.json → markdown table + grouped failure digest
```

Three boundaries, each understandable and testable in isolation:

- **Runner** — `runAgent({agent, workspaceDir, prompt, maxTurns}) → {transcript, tokens, costUsd,
  wallMs, turns, cleanExit}`. The only place agent-specific knowledge lives (each agent's headless
  invocation: `claude -p --output-format json`, `cursor-agent -p`, `gemini -p`, `codex exec`). Adding an
  agent = adding one adapter file.
- **Track** — provisions the workspace *before* the agent runs (pure file ops). Agnostic copies only the
  bundled `AGENTS.md` index + docs; native layers the agent's own delivery mechanism on top. The runner
  is track-agnostic.
- **Verifier** — each task's `verify.mjs` runs `dotnet build` → `dotnet ef database update` → `ng build`
  → a targeted Playwright spec, plus task-specific assertions (file exists / grep pattern / endpoint
  200). Returns graded checks; headline = "all `required` checks passed."

**Data flow:** `cli` → for each `(agent × track × task × rep)`: provision an isolated workspace (copy
fixture + apply track) → `runAgent` → `verify` → record a result row → aggregate to `matrix.json` →
`report.mjs` renders a table + a per-failure digest.

## Task format

A task is a self-contained folder; the corpus is a **graded mix** (atomic / feature / full-app):

```jsonc
// tasks/atomic/add-validator/task.json
{
  "id": "add-validator",
  "tier": "atomic",                   // atomic | feature | full-app
  "targets": ["validation"],          // which surface(s) this exercises → drives the failure digest
  "fixture": "e2e-fixtures",          // starting state; reuses tests/e2e-fixtures (a real Spiderly app)
  "maxTurns": 15,
  "required": ["compiles", "migrates"]// which verifier checks must pass for a green
}
```

The graded mix doubles as an attribution tool: when the full-app task fails but its constituent atomic
tasks pass, the gap is in *chaining/sequencing*, not in any single doc.

**Tier targets:** ~6–8 atomic (one per `doc`/`skill` surface), ~3 feature (multi-step, e.g. "add entity
X end-to-end with validation + a custom endpoint"), ~1 full-app (scaffold from a spec, exercising the
`init` / getting-started flow). Start with one per tier (walking skeleton) and grow.

## Context tracks and the `surface` tie-in

The agnostic context is **the bundle Spiderly already generates** — `build-agent-bundle.mjs` output
shipped as the `AGENTS.md` index + docs. This is deliberate: improving the eval score and improving the
shipped product become the *same action*.

| Agent | Agnostic track | Native track adds |
|-------|----------------|-------------------|
| Claude Code | `AGENTS.md` index + bundled docs | the 7 junctioned `skill`-surface skills (`.claude/skills/spiderly-*`) |
| Cursor | same | `.cursor/rules/*.mdc` generated from the same source |
| Gemini | same | `GEMINI.md` generated from the same source |
| Codex | same | `AGENTS.md` *is* Codex's native file → native ≈ agnostic |

**The agnostic↔native gap is the central measurement.** For Claude it equals *"what do the junctioned
skills add on top of the `AGENTS.md` docs index every agent already gets?"* — i.e. whether the `skill`
surfaces in `agent-surface.json` earn their place, the exact Vercel question on our own corpus. For Codex
(and largely Gemini) the gap is ~0 by construction, because their native mechanism *is* the agnostic
file — that is a finding, not a flaw: it quantifies that those agents have no extra leverage to offer.

**This directly feeds `agent-surface.json`.** Per-task `targets` + the agnostic↔native delta tell us, per
surface, whether `doc` or `skill` produces higher task success — resolving Open decision #3 (the deferred
`backend-hooks` / `authorization` placement) with data instead of a default.

**Neutralization (correctness-critical).** The agnostic track must suppress native auto-loading or it is
not agnostic: run Claude with **no** plugin/skill junctions present and the workspace `CLAUDE.md`
identical to (or importing only) `AGENTS.md`; do not drop `.cursor/rules`; etc. Otherwise an agent
silently gets its native layer during the "agnostic" run and the comparison is contaminated.

## Scoring, noise, and metrics

- **Headline score is binary and defensible.** Each verifier check is binary; the headline = "all
  `required` checks passed." Sub-checks are retained for *diagnosis only* ("compiled but e2e failed" is
  more actionable than a flat fail) and are **not** averaged into a fuzzy 0–100.
- **Stochasticity.** A single run is noise. Each task runs **N reps** (default 3 for the dev loop) and
  reports a **success rate** (e.g. 2/3) plus **variance**. A 3/3 or 0/3 is a clear signal; a flaky
  1/3↔2/3 between two bundle versions is flagged as needing more reps. Before declaring "new docs are
  better," bump to 5–10 reps so the delta clears the noise floor.
- **Metrics beyond pass/fail, first-class:** **cost** ($/tokens per task — a pass in 3 turns beats a
  pass in 12), **turns / wall-time** (efficiency proxy), and a **hack detector** that scans the
  transcript for `report-gap`-shaped signals (copied generated code, edited framework internals,
  bypassed the clean path). A *passing* task that required a hack is still a gap; this reuses the
  `report-gap` vocabulary already in the repo.
- **Reproducibility.** Every run records agent CLI version, model id, Spiderly commit, fixture commit,
  and corpus version into `matrix.json`. Agents upgrade models silently; without pinning, today's score
  is not comparable to next month's.
- **Contamination hygiene.** Do not paste exact task solutions into the bundled docs (that measures
  memorization). Keep a held-out task subset that never informs bundle edits, to catch overfitting.

## The phase-1 deliverable is the failure digest, not the score

`report.mjs` emits, per failure, `{task, failed check, transcript excerpt at the failure point, targets,
suspected gap}`, **grouped by `targets`** so a maintainer sees "validation → 4 failures across 2 agents,"
fixes that one surface, and re-measures. It can optionally auto-draft a `report-gap` issue.

## Self-test (validate the harness before trusting it)

Before any real (paid) run, validate the verifiers with two fake agents:

- an **oracle** that applies a known-good patch (`oracle/<task>/`) — must score **100%**;
- a **no-op** that does nothing — must score **~0%**.

If the no-op passes a task, that task's verifier is too lax; if the oracle fails, it is too strict. This
proves the harness *discriminates* before money is spent. The oracle patches double as living
documentation of each task's intended solution.

## Validation strategy (aligned with #250)

Mirrors what the distribution doc already decided, which in turn mirrors Vercel's `next-evals-oss`:

- **Live agent evals run out-of-band** — manual or `workflow_dispatch`, real API calls, real cost.
- **Results are committed** to the repo (and could later publish to a results page).
- **The only CI gate is a cheap deterministic invariant** — cache-completeness / "results committed /
  fresh," composing with the existing SSOT diff-guard model (`.githooks/pre-commit` + CI). No live agent
  run is ever wired into a blocking per-PR/per-release gate.

## Phased rollout

1. **Phase 1 — improve the bundle (walking skeleton).** `cli` + Claude adapter + agnostic track + 3
   tasks (one per tier) + oracle/no-op self-test + `report.mjs`. Run locally, Claude-only. Loop: run →
   read digest → fix the bundle → re-run. Grow to ~6–8 atomic / 3 feature / 1 full-app.
2. **Phase 2 — cross-agent.** Add cursor/gemini/codex adapters + native track + generated per-agent
   files (`.cursor/rules`, `GEMINI.md`). Run the matrix; produce the portability table + the
   agnostic↔native gap chart; feed per-surface deltas back into `agent-surface.json`. Moves to GitHub
   Actions (4 provider keys as CI secrets).
3. **Phase 3 — regression signal.** Commit results + the deterministic results-freshness gate
   (`workflow_dispatch` for live runs). No live blocking gate.

## Cost reality

The full matrix is 4 agents × 2 tracks × ~10 tasks × 3 reps = **~240 multi-turn agent runs**, each
compiling a .NET + Angular app. Mitigations are designed in:

- `--reps` / `--tier` / `--agents` flags are the budget dial per invocation.
- The full matrix runs nightly / on `workflow_dispatch`, never per-commit.
- Wall-time is dominated by `dotnet` / `ng` builds → cache nuget/npm and parallelize across runners.

## Considered and rejected

- **Adopt an eval framework (promptfoo / Inspect / OpenAI Evals) as the engine.** Built for
  prompt/API-level evals; the agentic part — let a CLI agent freely edit a repo over many turns, then
  judge by whether the project builds — fights their grain, and a fat dependency in an OSS framework repo
  is a smell. Keep them as an *optional later* reporting/UI layer (hybrid), not the engine.
- **LLM-judge as the headline.** Throws away the deterministic compiler/test signal that is Spiderly's
  main advantage.
- **Blocking live agent gate on PR/release.** Non-deterministic + costly → red on noise. Already rejected
  in the distribution doc.
- **Agnostic-only or native-only tracks.** Agnostic-only handicaps Claude (ignores the junctioned skills
  it really ships with); native-only confounds doc quality with per-agent tuning. The *gap between the
  two tracks* is the signal we actually want.

## Open decisions

1. **Spec location vs. the distribution doc.** This spec sits in `docs/superpowers/specs/` (brainstorming
   convention) while its parent, `docs/agent-guidance-distribution.md`, sits at the top of `docs/`.
   Acceptable (cross-linked), but consider promoting a condensed version to `docs/agent-evals.md`
   alongside its sibling once implementation starts.
2. **Fixture strategy for feature/full-app tiers.** Reuse `tests/e2e-fixtures` as-is, or vendor
   per-task fixture variants? Default: reuse + per-task setup steps; revisit if tasks need divergent
   starting states.
3. **Hack-detector precision.** Transcript scanning for `report-gap`-shaped fallbacks is heuristic;
   decide acceptable false-positive rate, or gate it behind manual review of flagged transcripts.

## References

- Parent design — [`docs/agent-guidance-distribution.md`](../../agent-guidance-distribution.md) ·
  issue [#250](https://github.com/filiptrivan/spiderly/issues/250)
- AGENTS.md outperforms skills in our agent evals —
  https://vercel.com/blog/agents-md-outperforms-skills-in-our-agent-evals
- Vercel eval suite — https://github.com/vercel/next-evals-oss · results https://nextjs.org/evals
- Next.js 16.2 AI improvements (version-pinned docs) — https://nextjs.org/blog/next-16-2-ai
- Task-based agent evals (build/test as ground truth) — SWE-bench, terminal-bench
- `AGENTS.md` convention — https://agents.md · `llms.txt` — https://llmstxt.org
