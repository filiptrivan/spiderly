import { join } from 'node:path';
import { copyDir, skipDirsFilter, BUILD_ARTIFACT_DIRS } from '../lib/fs-utils.mjs';
import { fixturesRoot } from '../lib/paths.mjs';

// Benchmark plain (no-Spiderly) track — DISTINCT from the showcase `plain` track, which provisions a
// BARE workspace for freestyle "make the app" builds. The SCORED benchmark instead starts the plain
// arm from the frozen thin plain-.NET baseline (auth + EF, no entity) so runs are repeatable and the
// agent only does per-feature work. The TRACK owns the starter (it ignores task.fixture); no Spiderly
// guidance is written. Build output is never provisioned. `root`/`fixtureName` injectable for tests.
export function provision(task, workspaceDir, { fixtureName = 'plain-app', root = fixturesRoot } = {}) {
  // Skip every build artifact (the thin baseline ships no node_modules to keep anyway).
  copyDir(join(root, fixtureName), workspaceDir, { filter: skipDirsFilter(BUILD_ARTIFACT_DIRS) });
}
