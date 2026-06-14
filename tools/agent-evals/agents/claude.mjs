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
