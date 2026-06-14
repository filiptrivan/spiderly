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
