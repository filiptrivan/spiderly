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
