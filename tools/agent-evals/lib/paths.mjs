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
