import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot } from '../lib/paths.mjs';

// Spiderly (with-framework) benchmark arm. The fixture is a REAL `spiderly init` app, left intact:
// its agent guidance — the root AGENTS.md, the docs under node_modules/spiderly/agent/docs, and the
// .claude/skills/spiderly-* skills — is already there because `spiderly init` runs `agent-sync`. So
// this track ADDS nothing and (unlike plain-net) STRIPS nothing but .NET build output: the agent
// gets exactly what a real Spiderly developer has. The TRACK owns the starter (ignores task.fixture),
// mirroring plain-net. `root`/`fixtureName` injectable for tests.
//
// KEEP node_modules — that's where the shipped docs/skills live; stripping it is the very bug we just
// fixed, one level up. Only .NET build artifacts (bin/obj) are skipped.
const SKIP = /[\\/](bin|obj)([\\/]|$)/;

export function provision(task, workspaceDir, { fixtureName = 'spiderly-app', root = fixturesRoot } = {}) {
  copyDir(join(root, fixtureName), workspaceDir, { filter: (src) => !SKIP.test(src) });
}
