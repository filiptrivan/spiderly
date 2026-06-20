import { join } from 'node:path';
import { copyDir, skipDirsFilter, BUILD_ARTIFACT_DIRS } from '../lib/fs-utils.mjs';
import { fixturesRoot } from '../lib/paths.mjs';

// Spiderly (with-framework) benchmark arm. The fixture is a REAL `spiderly init` app, left intact:
// its agent guidance — the root AGENTS.md, the docs under node_modules/spiderly/agent/docs, and the
// .claude/skills/spiderly-* skills — is already there because `spiderly init` runs `agent-sync`. So
// this track ADDS nothing and (unlike plain-net) STRIPS nothing but .NET build output: the agent
// gets exactly what a real Spiderly developer has. The TRACK owns the starter (ignores task.fixture),
// mirroring plain-net. `root`/`fixtureName` injectable for tests.
//
// KEEP node_modules — that's where the shipped docs/skills live; stripping it is the very bug we just
// fixed, one level up. So skip the standard build artifacts MINUS node_modules: the keep-it invariant
// is one data difference from the shared set, not a hand-retyped regex.
const SKIP_DIRS = new Set([...BUILD_ARTIFACT_DIRS].filter((d) => d !== 'node_modules'));

export function provision(task, workspaceDir, { fixtureName = 'spiderly-app', root = fixturesRoot } = {}) {
  copyDir(join(root, fixtureName), workspaceDir, { filter: skipDirsFilter(SKIP_DIRS) });
}
