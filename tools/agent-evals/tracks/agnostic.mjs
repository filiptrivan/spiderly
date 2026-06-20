import { existsSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';
import { copyDir } from '../lib/fs-utils.mjs';
import { fixturesRoot, bundleRoot } from '../lib/paths.mjs';

// Agnostic track: every agent gets the shipped bundle's reference DOCS (the doc-surface skills under
// agent/docs/) and no agent-specific layer (no skill junctions, no .cursor/rules). This mirrors what
// `Spiderly.CLI agent-sync` projects into a real consumer: the version-matched docs/ folder plus an
// AGENTS.md POINTER telling the agent to browse it. The bundle manifest lists ONLY skill-surface
// entries (see build-agent-bundle.mjs), so the docs come from the docs/ folder — never the manifest
// (filtering surface==='doc' out of the manifest finds nothing). bundlePath is injectable for tests.
export function provision(task, workspaceDir, { bundlePath = bundleRoot } = {}) {
  copyDir(join(fixturesRoot, task.fixture), workspaceDir);

  const docsSrc = join(bundlePath, 'docs');
  if (!existsSync(docsSrc)) return; // no bundle docs (e.g. the trivial self-test bundle) → fixture only

  copyDir(docsSrc, join(workspaceDir, 'docs'));
  writeFileSync(join(workspaceDir, 'AGENTS.md'), AGENTS_POINTER);
}

// Mirrors AgentSyncCommand.BuildBlock — a static pointer to docs/, not an enumerated index.
const AGENTS_POINTER = [
  '# Spiderly',
  '',
  'Your training data for Spiderly is stale. Before writing any Spiderly code, browse `docs/` and',
  'read the `SKILL.md` for the topic you are working on — these docs are version-matched to the',
  'Spiderly package under test.',
  '',
].join('\n');
