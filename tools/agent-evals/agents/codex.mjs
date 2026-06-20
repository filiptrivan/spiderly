import { run } from '../lib/exec.mjs';

// Parse Codex `exec --json` output. Codex emits a JSONL event stream (thread.started,
// turn.started, turn.completed, item.*, error) — NOT Claude's single JSON object. Token usage
// lives on `turn.completed` events; usage totals are cumulative per the session JSONL, so the
// run total is the LAST turn.completed's usage. (Needs confirming on a live run — if the --json
// stdout stream turns out to report per-turn deltas instead, switch to summing output_tokens.)
export function parseCodexUsage(stdout) {
  const completed = [];
  for (const line of stdout.split('\n')) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    let ev;
    try { ev = JSON.parse(trimmed); } catch { continue; } // ignore non-JSON / partial lines
    if (ev?.type === 'turn.completed') completed.push(ev);
  }
  const usage = completed[completed.length - 1]?.usage ?? {};
  return {
    tokens: usage.output_tokens ?? 0, // match claude.mjs: `tokens` = output tokens
    turns: completed.length,
    usage,
  };
}

// Real Codex headless run (`codex exec --json`). Edits the workspace in place over multiple turns.
// Auth: ChatGPT subscription sign-in (a CODEX subscription secret in CI). Subscription-only by
// design — do NOT add a per-use OpenAI API key.
// NOTE: Date.now() is fine here — this is an ordinary Node CLI, not a Workflow() sandbox.
//
// THREE THINGS TO CONFIRM AGAINST A LIVE CODEX BEFORE A REAL EVAL (deliberately not guessed):
//   1. Sandbox/approval flag — must allow unattended file edits AND running `dotnet build`
//      (incl. NuGet network restore). `--dangerously-bypass-approvals-and-sandbox` is the full
//      bypass that mirrors claude's `bypassPermissions`; verify the exact spelling + that network
//      is permitted on the installed version (`--full-auto` may block network restore).
//   2. Turn cap — Codex `exec` has no `--max-turns` equivalent of claude's; `maxTurns` is accepted
//      for interface parity but not passed. Confirm how Codex bounds a runaway session.
//   3. Cost — Codex emits NO dollar figure. costUsd stays 0 here; the run cost is derived centrally
//      from `usage` x list price at the reporting step (pricing.mjs, TODO).
export default {
  name: 'codex',
  async run({ workspaceDir, prompt, maxTurns }) {
    void maxTurns; // see note 2 — no Codex equivalent; kept for interface parity
    const start = Date.now();
    const res = run('codex', [
      'exec',
      '--json',
      // Eval workspaces are standalone dirs that may live outside a git repo (or in a temp dir);
      // `codex exec` refuses to run outside a repo without this flag. Harmless when nested in one.
      '--skip-git-repo-check',
      '--dangerously-bypass-approvals-and-sandbox',
      prompt,
    ], { cwd: workspaceDir, shell: process.platform === 'win32', timeoutMs: 20 * 60 * 1000 });
    const wallMs = Date.now() - start;

    const { tokens, turns, usage } = parseCodexUsage(res.stdout);

    // On failure, surface both streams — matrix.json keeps only agentMeta, so this log is the only
    // place the cause (auth/quota/crash) is visible; without it a failure looks like a 0-turn no-op.
    if (res.code !== 0) {
      console.error(`[codex] exit ${res.code}\n[stdout] ${res.stdout.trim().slice(-1500)}\n[stderr] ${res.stderr.trim().slice(-800)}`);
    }
    const transcript = res.stdout + (res.stderr.trim() ? `\n[stderr]\n${res.stderr.trim()}` : '');

    return { transcript, tokens, costUsd: 0, turns, wallMs, cleanExit: res.code === 0, usage };
  },
};
