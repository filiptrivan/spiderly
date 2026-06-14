import { existsSync } from 'node:fs';
import { join } from 'node:path';
import { pathToFileURL } from 'node:url';
import { run } from './lib/exec.mjs';

// Loads the task's own verify.mjs and runs it, passing the shared exec `run` helper so
// verifiers can shell out to dotnet/ng/playwright. Returns { checks: Check[] }.
//
// INVARIANT for task verifiers: a verify.mjs must NEVER throw on absent or garbage agent
// output — return a failing Check ({ pass: false }) instead. A thrown error is treated as an
// INFRA failure (pass:null) by the orchestrator and excluded from the agent's score.
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
