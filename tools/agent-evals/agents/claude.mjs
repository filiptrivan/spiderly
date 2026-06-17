import { run } from '../lib/exec.mjs';

// Real Claude Code headless run. Edits the workspace in place over multiple turns.
// Auth (any one): a logged-in CLI, ANTHROPIC_API_KEY, or a Pro/Max CLAUDE_CODE_OAUTH_TOKEN in env.
// NOTE: Date.now() is fine here — this is an ordinary Node CLI, not a Workflow() sandbox.
export default {
  name: 'claude',
  async run({ workspaceDir, prompt, maxTurns }) {
    const start = Date.now();
    // shell:false on POSIX so the multi-word `prompt` is passed as a SINGLE argv entry — under
    // shell:true the shell re-splits it into garbage args and `claude` exits immediately (0 turns).
    // Windows still needs shell:true to launch the `claude.cmd` shim (Node won't spawn .cmd without
    // a shell), where Node applies its own arg quoting.
    const res = run('claude', [
      '-p', prompt,
      '--output-format', 'json',
      '--max-turns', String(maxTurns ?? 20),
      '--permission-mode', 'bypassPermissions',
    ], { cwd: workspaceDir, shell: process.platform === 'win32', timeoutMs: 20 * 60 * 1000 });
    const wallMs = Date.now() - start;

    let meta = { tokens: 0, costUsd: 0, turns: 0 };
    try {
      const j = JSON.parse(res.stdout);
      meta = {
        tokens: j.usage?.output_tokens ?? 0,
        costUsd: j.total_cost_usd ?? 0,
        turns: j.num_turns ?? 0,
      };
    } catch { /* non-JSON stdout (e.g. startup crash) — keep defaults; the error is on stderr, below */ }

    // claude prints its run JSON to stdout but startup errors (bad auth/flags) to stderr — surface
    // those so a failed run is diagnosable instead of a silent 0-turns no-op.
    if (res.code !== 0 && res.stderr.trim()) {
      console.error(`[claude] exit ${res.code}: ${res.stderr.trim().slice(-1500)}`);
    }
    const transcript = res.stdout + (res.stderr.trim() ? `\n[stderr]\n${res.stderr.trim()}` : '');

    return { transcript, ...meta, wallMs, cleanExit: res.code === 0 };
  },
};
